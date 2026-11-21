namespace HawalekNow.Application.Modules.Merchants.Contracts;

// DTOs cross the boundary; neither EF entities nor Identity types do.
public sealed record MerchantAccountInfo(Guid Id, string Status, bool CanPrepareAccount, bool IsApproved, bool IsSuspended);

public interface IMerchantAccounts
{
    Task CreatePendingAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<MerchantAccountInfo?> FindByOwnerAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RemoveIdentityProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}
