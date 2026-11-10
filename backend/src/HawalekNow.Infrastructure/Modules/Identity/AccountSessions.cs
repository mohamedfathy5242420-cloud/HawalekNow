using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class LoginTiming
{
    private readonly ApplicationUser dummy = ApplicationUser.Create(AccountType.Customer, "dummy", "dummy@example.invalid");
    private readonly PasswordHasher<ApplicationUser> hasher = new();
    private readonly string hash;
    public LoginTiming() => hash = hasher.HashPassword(dummy, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
    public void Verify(string password) => hasher.VerifyHashedPassword(dummy, hash, password);
}

public sealed class AccountSessions(HawalekDbContext context, EmailConfirmationService accounts, IdentityStateLock stateLock, ILookupNormalizer normalizer,
    UserManager<ApplicationUser> users, LoginTiming timing, SessionTokens tokens, TimeProvider clock, IMerchantAccounts merchants) : IAccountSessions
{
    public async Task<Result<SessionCredentials>> LoginAsync(string accountType, string identifier, string password, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        ApplicationUser? user;
        if (string.Equals(accountType, "Merchant", StringComparison.OrdinalIgnoreCase) && UsernameRules.IsValid(identifier))
        {
            var normalized = normalizer.NormalizeName(identifier);
            user = await context.Set<ApplicationUser>().SingleOrDefaultAsync(value => value.AccountType == AccountType.Merchant && value.NormalizedUserName == normalized, cancellationToken);
        }
        else user = await accounts.FindAsync(accountType, identifier, cancellationToken);
        if (user is null) { timing.Verify(password); return Denied(); }
        await stateLock.AcquireAsync(user.Id, cancellationToken); await context.Entry(user).ReloadAsync(cancellationToken);
        if (context.Entry(user).State == EntityState.Detached) { timing.Verify(password); return Denied(); }
        var now = clock.GetUtcNow();
        if (user.LockoutEnd > now || user.AccessStatus != AccountAccessStatus.Active || !await MerchantAvailableAsync(user.AccountType, user.Id, cancellationToken))
        { timing.Verify(password); return Denied(); }
        if (!await users.CheckPasswordAsync(user, password))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= 5) { user.LockoutEnd = now.AddMinutes(15); user.AccessFailedCount = 0; }
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Denied();
        }
        user.AccessFailedCount = 0; user.LockoutEnd = null;
        var session = AccountSession.Create(user.Id, now); context.Add(session);
        var grant = Issue(user.Id, session, now);
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return Result<SessionCredentials>.Success(grant);
    }
    public async Task<Result<SessionCredentials>> RefreshAsync(string? credential, CancellationToken cancellationToken)
    {
        if (!SessionTokens.TryRead(credential, out var id, out var secret)) return Denied();
        var owner = await (from value in context.Set<RefreshCredential>().AsNoTracking()
                           join session in context.Set<AccountSession>() on value.SessionId equals session.Id
                           where value.Id == id
                           select (Guid?)session.UserId).SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return Denied();
        await using var transaction = await context.BeginAsync(cancellationToken);
        await stateLock.AcquireAsync(owner.Value, cancellationToken);
        var row = await context.Set<RefreshCredential>().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (row is null || !SessionTokens.Matches(row, secret)) return Denied();
        var sessionRow = await context.Set<AccountSession>().SingleAsync(value => value.Id == row.SessionId, cancellationToken);
        var now = clock.GetUtcNow();
        // A valid consumed credential proves reuse: revoke even when that credential's own expiry elapsed.
        if (row.ConsumedAt is not null)
        {
            sessionRow.Revoke(now); await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Denied();
        }
        var user = await context.Set<ApplicationUser>().SingleOrDefaultAsync(value => value.Id == owner.Value && value.AccessStatus == AccountAccessStatus.Active, cancellationToken);
        if (row.ExpiresAt <= now || user is null || !await MerchantAvailableAsync(user.AccountType, user.Id, cancellationToken) || !sessionRow.Rotate(now)) return Denied();
        row.ConsumedAt = now;
        var grant = Issue(owner.Value, sessionRow, now);
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return Result<SessionCredentials>.Success(grant);
    }
    public async Task<SessionActor?> FindActiveAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var actor = await (from session in context.Set<AccountSession>().AsNoTracking()
                           join user in context.Set<ApplicationUser>().AsNoTracking() on session.UserId equals user.Id
                           where session.Id == sessionId && user.Id == userId && user.AccessStatus == AccountAccessStatus.Active && session.RevokedAt == null && session.RefreshExpiresAt > now && session.AbsoluteExpiresAt > now
                           select new SessionActor(user.Id, session.Id, user.AccountType.ToString(), user.EmailConfirmed)).SingleOrDefaultAsync(cancellationToken);
        if (actor is null) return null;
        if (actor.AccountType == nameof(AccountType.Merchant) && !await MerchantAvailableAsync(AccountType.Merchant, userId, cancellationToken)) return null;
        return actor;
    }
    private async Task<bool> MerchantAvailableAsync(AccountType type, Guid userId, CancellationToken cancellationToken)
    {
        if (type != AccountType.Merchant) return true;
        var account = await merchants.FindByOwnerAsync(userId, cancellationToken);
        return account is { IsSuspended: false };
    }
    private SessionCredentials Issue(Guid userId, AccountSession session, DateTimeOffset now)
    {
        var refresh = SessionTokens.Refresh(session.Id, session.RefreshExpiresAt); context.Add(refresh.Row);
        var expiry = now.AddMinutes(5) < session.AbsoluteExpiresAt ? now.AddMinutes(5) : session.AbsoluteExpiresAt;
        return new(tokens.Access(userId, session.Id, now, expiry), refresh.Token, expiry, refresh.Row.ExpiresAt);
    }
    private static Result<SessionCredentials> Denied() => Result<SessionCredentials>.Failure(new(ErrorKind.Unauthorized));
}
