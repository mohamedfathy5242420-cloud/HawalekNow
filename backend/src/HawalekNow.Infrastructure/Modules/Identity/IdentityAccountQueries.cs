using HawalekNow.Application.Modules.Identity;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class IdentityAccountQueries(HawalekDbContext context, TimeProvider clock) : IIdentityAccountQueries
{
    public Task<CustomerIdentityInfo?> CustomerIdentityAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Set<ApplicationUser>().AsNoTracking().Where(user => user.Id == userId && user.AccountType == AccountType.Customer && user.EmailConfirmed && user.AccessStatus == AccountAccessStatus.Active)
            .Select(user => new CustomerIdentityInfo(user.Id, user.UserName!, user.Email!)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountSessionInfo>> SessionsAsync(Guid userId, Guid currentSessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await context.Set<AccountSession>().AsNoTracking().Where(session => session.UserId == userId && session.RevokedAt == null && session.RefreshExpiresAt > now && session.AbsoluteExpiresAt > now)
            .OrderByDescending(session => session.Id == currentSessionId).ThenByDescending(session => session.CreatedAt).Take(50)
            .Select(session => new AccountSessionInfo(session.Id, session.CreatedAt, session.LastRefreshedAt, session.RefreshExpiresAt, session.AbsoluteExpiresAt, session.RevokedAt, session.Id == currentSessionId))
            .ToArrayAsync(cancellationToken);
    }
    public Task<AccountSessionInfo?> SessionAsync(Guid userId, Guid sessionId, Guid currentSessionId, CancellationToken cancellationToken) =>
        context.Set<AccountSession>().AsNoTracking().Where(session => session.UserId == userId && session.Id == sessionId)
            .Select(session => new AccountSessionInfo(session.Id, session.CreatedAt, session.LastRefreshedAt, session.RefreshExpiresAt, session.AbsoluteExpiresAt, session.RevokedAt, session.Id == currentSessionId))
            .SingleOrDefaultAsync(cancellationToken);
}
