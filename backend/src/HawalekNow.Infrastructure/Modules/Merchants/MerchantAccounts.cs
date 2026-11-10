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
}

public static class MerchantRegistration
{
    public static IServiceCollection AddMerchantAccounts(this IServiceCollection services) =>
        services.AddScoped<IMerchantAccounts, MerchantAccounts>();
}
