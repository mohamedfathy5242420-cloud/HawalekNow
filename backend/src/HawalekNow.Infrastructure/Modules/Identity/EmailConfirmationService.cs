using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class EmailConfirmationService(HawalekDbContext context, ILookupNormalizer normalizer, OtpProtection secrets,
    PayloadProtection protection, OutboxWriter outbox, IdentityStateLock stateLock, TimeProvider clock) : IEmailConfirmation
{
    public async Task<Result> RequestAsync(string accountType, string email, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        var user = await FindAsync(accountType, email, cancellationToken);
        if (user is not null)
        {
            await stateLock.AcquireAsync(user.Id, cancellationToken);
            await context.Entry(user).ReloadAsync(cancellationToken);
            if (context.Entry(user).State != EntityState.Detached && user.AccessStatus == AccountAccessStatus.Active && !user.EmailConfirmed)
                await IssueInsideTransactionAsync(user, OtpPurpose.EmailConfirmation, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> VerifyAsync(string accountType, string email, string code, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        var user = await FindAsync(accountType, email, cancellationToken);
        if (user is null) return Invalid();
        await stateLock.AcquireAsync(user.Id, cancellationToken); await context.Entry(user).ReloadAsync(cancellationToken);
        if (context.Entry(user).State == EntityState.Detached) return Invalid();
        var challenge = await context.Set<OtpChallenge>().SingleOrDefaultAsync(value => value.UserId == user.Id && value.Purpose == OtpPurpose.EmailConfirmation, cancellationToken);
        var accepted = user.AccessStatus == AccountAccessStatus.Active && !user.EmailConfirmed && challenge is not null && challenge.Consume(secrets.Matches(challenge, code), clock.GetUtcNow());
        if (accepted) user.EmailConfirmed = true;
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return accepted ? Result.Success() : Invalid();
    }

    public async Task<bool> IssueInsideTransactionAsync(ApplicationUser user, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Challenge issuance requires a SQL transaction.");
        await stateLock.AcquireAsync(user.Id, cancellationToken);
        if (purpose == OtpPurpose.AccountRecovery ? user.AccessStatus != AccountAccessStatus.Deleted || user.AccountType == AccountType.Admin : user.AccessStatus != AccountAccessStatus.Active) return false;
        var challenge = await context.Set<OtpChallenge>().SingleOrDefaultAsync(value => value.UserId == user.Id && value.Purpose == purpose, cancellationToken);
        if (challenge is null) { challenge = OtpChallenge.Create(user.Id, purpose); context.Add(challenge); }
        var previous = await context.Set<OtpChallenge>().Where(value => value.UserId == user.Id).ToArrayAsync(cancellationToken);
        string code;
        do { code = secrets.Generate(); } while (previous.Any(value => secrets.Matches(value, code)));
        var generation = Guid.CreateVersion7();
        if (!challenge.Issue(generation, secrets.Hash(user.Id, purpose, generation, code), clock.GetUtcNow())) return false;
        outbox.Add(EmailOtpHandler.MessageKind, protection.Protect(EmailOtpHandler.MessageKind, new EmailOtpPayload(user.Id, purpose, generation, user.Email!, code)), user.Id);
        return true;
    }
    public Task<ApplicationUser?> FindAsync(string accountType, string email, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AccountType>(accountType, true, out var type) || !Enum.IsDefined(type)) return Task.FromResult<ApplicationUser?>(null);
        var normalized = normalizer.NormalizeEmail(email.Trim());
        return context.Set<ApplicationUser>().SingleOrDefaultAsync(value => value.AccountType == type && value.NormalizedEmail == normalized, cancellationToken);
    }
    private static Result Invalid() => Result.Failure(new(ErrorKind.Validation));
}
