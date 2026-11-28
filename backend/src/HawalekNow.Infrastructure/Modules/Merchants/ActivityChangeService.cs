using System.Collections.Immutable;
using HawalekNow.Application.Modules.Catalog.Contracts;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class ActivityChangeService(HawalekDbContext context, MerchantStoreAccess access, IAccountAccess identities, IActivityDirectory activities, TimeProvider clock) : IActivityChangeService
{
    public async Task<Result<ActivityChangeDto>> RequestAsync(Guid actorId, Guid storeId, ActivityChangeWrite value, CancellationToken cancellationToken)
    {
        if (storeId == Guid.Empty || !ActivityChangeInput.Write(value)) return Fail<ActivityChangeDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var owned = await access.OwnStoreAsync(actorId, storeId, true, cancellationToken);
            if (!owned.IsSuccess) return Result<ActivityChangeDto>.Failure(owned.Error!);
            var store = owned.Value;
            if (!Matches(store.RowVersion, value.StoreRowVersion)) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
            if (store.ActivityId == value.ActivityId || !await activities.CanSelectInsideTransactionAsync(value.ActivityId, cancellationToken)) return Fail<ActivityChangeDto>(ErrorKind.Validation);
            if (await context.Set<StoreActivityChange>().AnyAsync(item => item.StoreId == storeId && item.Status == ActivityChangeStatus.Pending, cancellationToken)) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
            var now = clock.GetUtcNow(); var request = StoreActivityChange.Create(storeId, store.ActivityId, value.ActivityId, actorId, value.Reason, now); context.Add(request);
            // These IDs describe the proposal. Store.ActivityId is unchanged until approval.
            Audit(actorId, "Store.ActivityChange.Request", store.Id, value.Reason, now, store.ActivityId, request.RequestedActivityId);
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result<ActivityChangeDto>.Success(ToDto(request, store));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Fail<ActivityChangeDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (Conflict(error)) { context.ChangeTracker.Clear(); return Fail<ActivityChangeDto>(ErrorKind.Conflict); }
    }
    public async Task<Result<ActivityChangeDto>> ReviewAsync(Guid actorId, Guid id, bool approve, ActivityChangeReviewWrite value, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !ActivityChangeInput.Review(value, approve)) return Fail<ActivityChangeDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            if (!Admin(await identities.FindForMutationAsync(actorId, cancellationToken))) return Fail<ActivityChangeDto>(ErrorKind.Forbidden);
            var owner = await (from request in context.Set<StoreActivityChange>().AsNoTracking()
                               join store in context.Set<Store>().AsNoTracking() on request.StoreId equals store.Id
                               join account in context.Set<MerchantAccount>().AsNoTracking() on store.MerchantAccountId equals account.Id
                               where request.Id == id
                               select (Guid?)account.UserId).SingleOrDefaultAsync(cancellationToken);
            if (owner is null) return Fail<ActivityChangeDto>(ErrorKind.NotFound);
            if (await identities.FindForMutationAsync(owner.Value, cancellationToken) is not { AccountType: "Merchant", EmailConfirmed: true, IsActive: true }) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
            var change = await context.Set<StoreActivityChange>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (change is null) return Fail<ActivityChangeDto>(ErrorKind.NotFound);
            await context.Entry(change).ReloadAsync(cancellationToken);
            var ownedStore = await context.Set<Store>().SingleOrDefaultAsync(item => item.Id == change.StoreId, cancellationToken);
            if (ownedStore is null) return Fail<ActivityChangeDto>(ErrorKind.NotFound);
            await context.Entry(ownedStore).ReloadAsync(cancellationToken);
            if (!Matches(change.RowVersion, value.RowVersion) || !Matches(ownedStore.RowVersion, value.StoreRowVersion) || change.Status != ActivityChangeStatus.Pending) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
            var before = ownedStore.ActivityId;
            if (approve)
            {
                if (before != change.PreviousActivityId) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
                if (!await activities.CanSelectInsideTransactionAsync(change.RequestedActivityId, cancellationToken)) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
                // P05 must include incompatible-product drafting in THIS outer transaction before enabling products.
                if (!ownedStore.TryApplyApprovedActivity(change.PreviousActivityId, change.RequestedActivityId)) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
            }
            var now = clock.GetUtcNow();
            if (!change.TryReview(approve, actorId, value.Reason, now)) return Fail<ActivityChangeDto>(ErrorKind.Conflict);
            Audit(actorId, approve ? "Store.ActivityChange.Approve" : "Store.ActivityChange.Reject", ownedStore.Id, value.Reason, now, before, ownedStore.ActivityId);
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result<ActivityChangeDto>.Success(ToDto(change, ownedStore));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Fail<ActivityChangeDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (Conflict(error)) { context.ChangeTracker.Clear(); return Fail<ActivityChangeDto>(ErrorKind.Conflict); }
    }
    public async Task<Result<MerchantPage<ActivityChangeDto>>> OwnListAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (storeId == Guid.Empty || !MerchantReviewInput.Page(page, pageSize)) return Fail<MerchantPage<ActivityChangeDto>>(ErrorKind.Validation);
        var owned = await access.OwnStoreAsync(actorId, storeId, false, cancellationToken);
        if (!owned.IsSuccess) return Result<MerchantPage<ActivityChangeDto>>.Failure(owned.Error!);
        return Result<MerchantPage<ActivityChangeDto>>.Success(await PageAsync(context.Set<StoreActivityChange>().Where(value => value.StoreId == storeId), page, pageSize, cancellationToken));
    }
    public async Task<Result<MerchantPage<ActivityChangeDto>>> AdminListAsync(Guid actorId, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!Admin(await identities.FindAsync(actorId, cancellationToken))) return Fail<MerchantPage<ActivityChangeDto>>(ErrorKind.Forbidden);
        if (!ActivityChangeInput.Status(status) || !MerchantReviewInput.Page(page, pageSize)) return Fail<MerchantPage<ActivityChangeDto>>(ErrorKind.Validation);
        var query = context.Set<StoreActivityChange>().AsNoTracking();
        if (status is not null) { var selected = Enum.Parse<ActivityChangeStatus>(status, true); query = query.Where(value => value.Status == selected); }
        return Result<MerchantPage<ActivityChangeDto>>.Success(await PageAsync(query, page, pageSize, cancellationToken));
    }
    private async Task<MerchantPage<ActivityChangeDto>> PageAsync(IQueryable<StoreActivityChange> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var rows = await (from change in query.AsNoTracking()
                          join store in context.Set<Store>().AsNoTracking() on change.StoreId equals store.Id
                          orderby change.RequestedAt descending, change.Id
                          select new { Change = change, Store = store }).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
        return new(rows.Take(pageSize).Select(row => ToDto(row.Change, row.Store)).ToImmutableArray(), page, pageSize, rows.Length > pageSize);
    }
    private void Audit(Guid actorId, string operation, Guid id, string? reason, DateTimeOffset now, Guid before, Guid after) => context.Add(AuditEntry.Record(actorId, operation, "Store", id, reason, now, before.ToString("N"), after.ToString("N")));
    private static bool Matches(byte[] actual, string expected) => actual.AsSpan().SequenceEqual(Convert.FromBase64String(expected));
    private static bool Admin(AccountAccessInfo? identity) => identity is { AccountType: "Admin", EmailConfirmed: true, IsActive: true };
    private static bool Conflict(SqlException error) => error.Number is 1205 or 1222 or 51003 or 51010;
    private static Result<T> Fail<T>(ErrorKind kind) => Result<T>.Failure(new(kind));
    private static ActivityChangeDto ToDto(StoreActivityChange change, Store store) => new(change.Id, store.Id, change.PreviousActivityId, change.RequestedActivityId, store.ActivityId,
        change.Status.ToString(), change.RequestedBy, change.RequestedAt, change.RequestReason, change.ReviewedBy, change.ReviewedAt, change.ReviewReason,
        Convert.ToBase64String(change.RowVersion), Convert.ToBase64String(store.RowVersion));
}
