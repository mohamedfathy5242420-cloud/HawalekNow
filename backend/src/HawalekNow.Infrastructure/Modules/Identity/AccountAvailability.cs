using HawalekNow.Application.Modules.Merchants.Contracts;
using HawalekNow.Domain.Modules.Identity;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class AccountAvailability(IMerchantAccounts merchants)
{
    public async Task<bool> IsActiveAsync(ApplicationUser user, CancellationToken cancellationToken) =>
        user.AccessStatus == AccountAccessStatus.Active && await MerchantAvailableAsync(user.AccountType, user.Id, cancellationToken);
    public async Task<bool> MerchantAvailableAsync(AccountType type, Guid userId, CancellationToken cancellationToken)
    {
        if (type != AccountType.Merchant) return true;
        return await merchants.FindByOwnerAsync(userId, cancellationToken) is { IsSuspended: false };
    }
}
