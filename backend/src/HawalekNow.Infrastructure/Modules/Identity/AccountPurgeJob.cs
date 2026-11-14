using Hangfire;
using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Application.Shared.Messaging;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed record DeletionSchedulePayload(Guid UserId, Guid Generation);
public sealed class DeletionScheduleHandler(HawalekDbContext context, IdentityStateLock stateLock, PayloadProtection protection, IBackgroundJobClient jobs) : IOutboxHandler
{
    public const string MessageKind = "identity.deletion-schedule.v1";
    public string Kind => MessageKind;
    public async Task<OutboxHandlingResult> HandleAsync(Guid messageId, string protectedPayload, CancellationToken cancellationToken)
    {
        var payload = protection.Unprotect<DeletionSchedulePayload>(Kind, protectedPayload);
        await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(payload.UserId, cancellationToken);
        var deletion = await context.Set<AccountDeletion>().SingleOrDefaultAsync(value => value.UserId == payload.UserId, cancellationToken);
        if (deletion is null || deletion.Generation != payload.Generation || deletion.RestoredAt is not null ||
            !await context.Set<ApplicationUser>().AnyAsync(value => value.Id == payload.UserId && value.AccountType != AccountType.Admin && value.AccessStatus == AccountAccessStatus.Deleted, cancellationToken)) return OutboxHandlingResult.Suppressed;
        if (deletion.ScheduledJobId is null)
        {
            // Hangfire commits separately. Lost acknowledgement can duplicate a job; the purge guard is authoritative.
            var jobId = jobs.Schedule<AccountPurgeJob>(job => job.RunAsync(payload.UserId, payload.Generation, CancellationToken.None), deletion.PurgeAt);
            deletion.RecordSchedule(jobId); await context.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken); return OutboxHandlingResult.Completed;
    }
}

public sealed class AccountPurgeJob(HawalekDbContext context, IdentifierLocks identifiers, IdentifierDigests digests,
    IdentityStateLock stateLock, IMerchantAccounts merchants, PayloadProtection protection, TimeProvider clock)
{
    [Queue("identity")]
    [AutomaticRetry(Attempts = 10)]
    public async Task RunAsync(Guid userId, Guid generation, CancellationToken cancellationToken)
    {
        try { await PurgeAsync(userId, generation, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { throw new InvalidOperationException("Account purge failed."); }
    }
    private async Task PurgeAsync(Guid userId, Guid generation, CancellationToken cancellationToken)
    {
        var observed = await context.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (observed is null || observed.AccountType == AccountType.Admin) return;
        await using var transaction = await context.BeginAsync(cancellationToken);
        // This order also matches registration. Recovery holds only the state lock, never identifier locks.
        await identifiers.AcquireAsync(observed.AccountType, observed.NormalizedUserName!, observed.NormalizedEmail!, cancellationToken);
        await stateLock.AcquireAsync(userId, cancellationToken);
        var user = await context.Set<ApplicationUser>().SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        var deletion = await context.Set<AccountDeletion>().SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        var now = clock.GetUtcNow();
        if (user is null || user.AccountType == AccountType.Admin || user.AccessStatus != AccountAccessStatus.Deleted || deletion?.CanPurge(generation, now) != true) return;
        if (user.NormalizedUserName != observed.NormalizedUserName || user.NormalizedEmail != observed.NormalizedEmail || user.AccountType != observed.AccountType)
            throw new InvalidOperationException("Account identifiers changed during purge.");
        await ClearPayloadsAsync(userId, cancellationToken);
        foreach (var pair in new[] { (IdentifierKind.Username, user.NormalizedUserName!), (IdentifierKind.Email, user.NormalizedEmail!) })
        {
            var digest = digests.Hash(user.AccountType, pair.Item1, pair.Item2);
            if (!await context.Set<ReservedIdentifier>().AnyAsync(value => value.AccountType == user.AccountType && value.Kind == pair.Item1 && value.Digest == digest, cancellationToken))
                context.Add(ReservedIdentifier.Create(user.AccountType, pair.Item1, digest));
        }
        if (user.AccountType == AccountType.Merchant) await merchants.RemoveIdentityProfileAsync(userId, cancellationToken);
        context.Add(AccountDeletionAudit.Create(userId, generation, AccountDeletionEvent.Purged, now));
        context.Remove(user);
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
    }
    private async Task ClearPayloadsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Pre-S09 rows have no subject metadata. Associate encrypted legacy OTPs before clearing only this owner.
        var legacy = await context.Set<OutboxMessage>().Where(value => value.SubjectId == null && value.Kind == EmailOtpHandler.MessageKind && value.ProtectedPayload != "").ToArrayAsync(cancellationToken);
        foreach (var message in legacy) message.SubjectId = protection.Unprotect<EmailOtpPayload>(message.Kind, message.ProtectedPayload).UserId;
        await context.SaveChangesAsync(cancellationToken);
        await context.Set<OutboxMessage>().Where(value => value.SubjectId == userId && value.ProtectedPayload != "" &&
                (value.Kind == EmailOtpHandler.MessageKind || value.Kind == DeletionScheduleHandler.MessageKind))
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.ProtectedPayload, "").SetProperty(value => value.State, OutboxState.Suppressed)
                .SetProperty(value => value.LeaseId, (Guid?)null).SetProperty(value => value.LeaseUntil, (DateTimeOffset?)null).SetProperty(value => value.LastFailureCode, (string?)null), cancellationToken);
    }
}
