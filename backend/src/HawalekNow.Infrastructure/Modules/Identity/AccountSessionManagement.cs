using HawalekNow.Application.Modules.Identity;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class AccountSessionManagement(HawalekDbContext context, IdentityStateLock stateLock, TimeProvider clock) : IAccountSessionManagement
{
    public async Task<Result> LogoutAsync(string? refreshCredential, CancellationToken cancellationToken)
    {
        if (!SessionTokens.TryRead(refreshCredential, out var id, out var secret)) return Result.Success();
        var owner = await (from token in context.Set<RefreshCredential>().AsNoTracking()
                           join session in context.Set<AccountSession>() on token.SessionId equals session.Id
                           where token.Id == id
                           select (Guid?)session.UserId).SingleOrDefaultAsync(cancellationToken);
        if (owner is null) return Result.Success();
        await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(owner.Value, cancellationToken);
        var credential = await context.Set<RefreshCredential>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (credential is null || !SessionTokens.Matches(credential, secret)) return Result.Success();
        var sessionRow = await context.Set<AccountSession>().SingleOrDefaultAsync(value => value.Id == credential.SessionId && value.UserId == owner.Value, cancellationToken);
        sessionRow?.Revoke(clock.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    public async Task<Result> LogoutAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(userId, cancellationToken);
        await RevokeAllInsideTransactionAsync(userId, cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    public async Task<Result> RevokeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken); await stateLock.AcquireAsync(userId, cancellationToken);
        var session = await context.Set<AccountSession>().SingleOrDefaultAsync(value => value.Id == sessionId && value.UserId == userId, cancellationToken);
        if (session is null) return Result.Failure(new(ErrorKind.NotFound));
        session.Revoke(clock.GetUtcNow());
        await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result.Success();
    }
    public Task<int> RevokeAllInsideTransactionAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Session revocation requires the owning SQL transaction.");
        var now = clock.GetUtcNow();
        return context.Set<AccountSession>().Where(value => value.UserId == userId && value.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.RevokedAt, now), cancellationToken);
    }
}
