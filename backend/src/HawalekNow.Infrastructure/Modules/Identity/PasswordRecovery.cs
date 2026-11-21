using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class PasswordRecovery(HawalekDbContext context, EmailConfirmationService accounts, IdentityStateLock stateLock, AccountAvailability availability,
    OtpProtection secrets, UserManager<ApplicationUser> users, AccountSessionManagement sessions, TimeProvider clock) : IPasswordRecovery
{
    public async Task<Result> RequestAsync(string accountType, string email, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        var user = await accounts.FindAsync(accountType, email, cancellationToken);
        if (user is not null)
        {
            await stateLock.AcquireAsync(user.Id, cancellationToken); await context.Entry(user).ReloadAsync(cancellationToken);
            if (context.Entry(user).State != EntityState.Detached && await availability.IsActiveAsync(user, cancellationToken))
                await accounts.IssueInsideTransactionAsync(user, OtpPurpose.PasswordReset, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    public async Task<Result> ResetAsync(string accountType, string email, string code, string newPassword, CancellationToken cancellationToken)
    {
        if (!PasswordRules.IsAcceptable(newPassword)) return Invalid();
        await using var transaction = await context.BeginAsync(cancellationToken);
        var user = await accounts.FindAsync(accountType, email, cancellationToken);
        if (user is null) return Invalid();
        await stateLock.AcquireAsync(user.Id, cancellationToken); await context.Entry(user).ReloadAsync(cancellationToken);
        if (context.Entry(user).State == EntityState.Detached || !await availability.IsActiveAsync(user, cancellationToken)) return Invalid();
        var challenge = await context.Set<OtpChallenge>().SingleOrDefaultAsync(value => value.UserId == user.Id && value.Purpose == OtpPurpose.PasswordReset, cancellationToken);
        if (challenge is null) return Invalid();
        if (!challenge.Consume(secrets.Matches(challenge, code), clock.GetUtcNow()))
        {
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Invalid();
        }
        // Both Identity store saves enlist in this transaction; failure after removal rolls back hash and challenge.
        if (!(await users.RemovePasswordAsync(user)).Succeeded || !(await users.AddPasswordAsync(user, newPassword)).Succeeded) return Invalid();
        user.AccessFailedCount = 0; user.LockoutEnd = null;
        await sessions.RevokeAllInsideTransactionAsync(user.Id, cancellationToken);
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    private static Result Invalid() => Result.Failure(new(ErrorKind.Validation));
}
