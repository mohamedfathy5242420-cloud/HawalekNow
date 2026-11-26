using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class MerchantStoreAccess(HawalekDbContext context, IAccountAccess identities)
{
    public async Task<Result<MerchantAccount>> OwnerAsync(Guid actorId, bool mutation, CancellationToken cancellationToken)
    {
        var identity = mutation ? await identities.FindForMutationAsync(actorId, cancellationToken) : await identities.FindAsync(actorId, cancellationToken);
        if (!ConfirmedMerchant(identity)) return Result<MerchantAccount>.Failure(new(ErrorKind.Forbidden));
        var account = await context.Set<MerchantAccount>().AsNoTracking().SingleOrDefaultAsync(value => value.UserId == actorId, cancellationToken);
        if (account is null) return Result<MerchantAccount>.Failure(new(ErrorKind.NotFound));
        if (!account.CanPrepareAccount) return Result<MerchantAccount>.Failure(new(ErrorKind.Forbidden));
        return Result<MerchantAccount>.Success(account);
    }
    public async Task<Result<Store>> OwnStoreAsync(Guid actorId, Guid id, bool mutation, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(actorId, mutation, cancellationToken);
        if (!owner.IsSuccess) return Result<Store>.Failure(owner.Error!);
        var query = context.Set<Store>().Where(value => value.Id == id && value.MerchantAccountId == owner.Value.Id);
        var store = await (mutation ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
        if (store is null) return Result<Store>.Failure(new(ErrorKind.NotFound));
        if (mutation) await context.Entry(store).ReloadAsync(cancellationToken);
        return Result<Store>.Success(store);
    }
    public async Task<bool> PublicAsync(Store store, CancellationToken cancellationToken)
    {
        var userId = await context.Set<MerchantAccount>().AsNoTracking().Where(value => value.Id == store.MerchantAccountId && value.Status == MerchantApprovalStatus.Approved)
            .Select(value => (Guid?)value.UserId).SingleOrDefaultAsync(cancellationToken);
        return userId is not null && ConfirmedMerchant(await identities.FindAsync(userId.Value, cancellationToken));
    }
    private static bool ConfirmedMerchant(AccountAccessInfo? value) => value is { AccountType: "Merchant", EmailConfirmed: true, IsActive: true };
}
