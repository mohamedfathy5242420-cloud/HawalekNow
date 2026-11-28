using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class AccountAccess(HawalekDbContext context, IdentityStateLock stateLock) : IAccountAccess
{
    public async Task<AccountAccessInfo?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Set<ApplicationUser>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null ? null : new(user.Id, user.AccountType.ToString(), user.EmailConfirmed,
            user.AccessStatus == AccountAccessStatus.Active, user.UserName, user.Email);
    }

    public async Task<AccountAccessInfo?> FindForMutationAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Account access mutation requires the owning transaction.");
        await stateLock.AcquireAsync(userId, cancellationToken);
        return await FindAsync(userId, cancellationToken);
    }
}
