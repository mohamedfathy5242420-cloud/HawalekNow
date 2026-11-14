using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class MerchantAccounts(HawalekDbContext context) : IMerchantAccounts
{
    public Task CreatePendingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        context.Set<MerchantAccount>().Add(MerchantAccount.CreatePending(userId));
        return Task.CompletedTask;
    }

    public async Task<MerchantAccountInfo?> FindByOwnerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await context.Set<MerchantAccount>().AsNoTracking().SingleOrDefaultAsync(value => value.UserId == userId, cancellationToken);
        return account is null ? null : new(account.Id, account.Status.ToString(), account.CanPrepareAccount,
            account.Status == MerchantApprovalStatus.Approved, account.Status == MerchantApprovalStatus.Suspended);
    }
    public Task RemoveIdentityProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null) throw new InvalidOperationException("Profile removal requires the owning SQL transaction.");
        // P02 owns only this empty identity profile. Future commercial data needs its own retention policy.
        return context.Set<MerchantAccount>().Where(value => value.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}

public static class MerchantRegistration
{
    public static IServiceCollection AddMerchantAccounts(this IServiceCollection services) =>
        services.AddScoped<IMerchantAccounts, MerchantAccounts>();
}
