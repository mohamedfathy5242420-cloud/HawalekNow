using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class AccountLifecycle(HawalekDbContext context, IdentityStateLock stateLock, EmailConfirmationService accounts,
    UserManager<ApplicationUser> users, AccountSessionManagement sessions, OtpProtection secrets,
    OutboxWriter outbox, PayloadProtection protection, TimeProvider clock) : IAccountLifecycle
{
    public async Task<Result> DeleteAsync(Guid userId, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(userId, cancellationToken);
        var user = await context.Set<ApplicationUser>().SingleOrDefaultAsync(value => value.Id == userId, cancellationToken);
        if (user is null) return Result.Failure(new(ErrorKind.Unauthorized));
        if (user.AccountType == AccountType.Admin) return Result.Failure(new(ErrorKind.Forbidden));
        if (user.AccessStatus == AccountAccessStatus.Deleted) return Result.Success(); // Never extend a pending deadline.
        var now = clock.GetUtcNow();
        if (user.AccessStatus != AccountAccessStatus.Active || user.LockoutEnd > now) return Result.Failure(new(ErrorKind.Unauthorized));
        if (!await users.CheckPasswordAsync(user, password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5) { user.LockoutEnd = now.AddMinutes(15); user.AccessFailedCount = 0; }
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Result.Failure(new(ErrorKind.Unauthorized));
        }
        var deletion = await context.Set<AccountDeletion>().SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        if (deletion is null) { deletion = AccountDeletion.Create(userId, user.AccountType, now); context.Add(deletion); }
        else deletion.Request(user.AccountType, now);
        user.MarkDeleted(); user.AccessFailedCount = 0; user.LockoutEnd = null;
        await sessions.RevokeAllInsideTransactionAsync(userId, cancellationToken); await InvalidateChallengesAsync(userId, now, cancellationToken);
        context.Add(AccountDeletionAudit.Create(userId, deletion.Generation, AccountDeletionEvent.Requested, now));
        outbox.Add(DeletionScheduleHandler.MessageKind, protection.Protect(DeletionScheduleHandler.MessageKind, new DeletionSchedulePayload(userId, deletion.Generation)), userId);
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    public async Task<Result> RequestRecoveryAsync(string accountType, string email, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        var user = await accounts.FindAsync(accountType, email, cancellationToken);
        if (user is not null)
        {
            await stateLock.AcquireAsync(user.Id, cancellationToken); await context.Entry(user).ReloadAsync(cancellationToken);
            if (context.Entry(user).State != EntityState.Detached && user.AccountType != AccountType.Admin && user.AccessStatus == AccountAccessStatus.Deleted)
            {
                var deletion = await context.Set<AccountDeletion>().SingleOrDefaultAsync(value => value.UserId == user.Id, cancellationToken);
                if (deletion?.CanRecover(clock.GetUtcNow()) == true) await accounts.IssueInsideTransactionAsync(user, OtpPurpose.AccountRecovery, cancellationToken);
            }
        }
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    public async Task<Result> RecoverAsync(string accountType, string email, string code, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        var user = await accounts.FindAsync(accountType, email, cancellationToken);
        if (user is null) return Invalid();
        await stateLock.AcquireAsync(user.Id, cancellationToken); await context.Entry(user).ReloadAsync(cancellationToken);
        if (context.Entry(user).State == EntityState.Detached || user.AccountType == AccountType.Admin || user.AccessStatus != AccountAccessStatus.Deleted) return Invalid();
        var now = clock.GetUtcNow();
        var deletion = await context.Set<AccountDeletion>().SingleOrDefaultAsync(value => value.UserId == user.Id, cancellationToken);
        var challenge = await context.Set<OtpChallenge>().SingleOrDefaultAsync(value => value.UserId == user.Id && value.Purpose == OtpPurpose.AccountRecovery, cancellationToken);
        if (deletion?.CanRecover(now) != true || challenge is null) return Invalid();
        if (!challenge.Consume(secrets.Matches(challenge, code), now))
        { await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Invalid(); }
        deletion.Restore(now); user.RestoreDeleted();
        await sessions.RevokeAllInsideTransactionAsync(user.Id, cancellationToken); await InvalidateChallengesAsync(user.Id, now, cancellationToken);
        context.Add(AccountDeletionAudit.Create(user.Id, deletion.Generation, AccountDeletionEvent.Recovered, now));
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    private async Task InvalidateChallengesAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var challenge in await context.Set<OtpChallenge>().Where(value => value.UserId == userId).ToArrayAsync(cancellationToken)) challenge.Invalidate(now);
    }
    private static Result Invalid() => Result.Failure(new(ErrorKind.Validation));
}
