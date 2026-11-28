using System.Collections.Immutable;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class StoreAdministration(HawalekDbContext context, MerchantStoreAccess access, IAccountAccess identities, StoreService stores, BranchService branches, TimeProvider clock) : IStoreAdministration
{
    public async Task<Result<AdminStoreDto>> ReadAsync(Guid actorId, Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return Fail<AdminStoreDto>(ErrorKind.Validation);
        var result = await access.AdminStoreAsync(actorId, id, false, cancellationToken);
        if (!result.IsSuccess) return Result<AdminStoreDto>.Failure(result.Error!);
        var account = await context.Set<MerchantAccount>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == result.Value.MerchantAccountId, cancellationToken);
        if (account is null) return Fail<AdminStoreDto>(ErrorKind.NotFound);
        return Result<AdminStoreDto>.Success(await ToDtoAsync(result.Value, account, cancellationToken));
    }
    public async Task<Result<MerchantPage<AdminStoreDto>>> ListAsync(Guid actorId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync(actorId, cancellationToken)) return Fail<MerchantPage<AdminStoreDto>>(ErrorKind.Forbidden);
        if (!MerchantReviewInput.Page(page, pageSize)) return Fail<MerchantPage<AdminStoreDto>>(ErrorKind.Validation);
        var rows = await (from store in context.Set<Store>().AsNoTracking()
                          join account in context.Set<MerchantAccount>().AsNoTracking() on store.MerchantAccountId equals account.Id
                          orderby store.Id
                          select new { Store = store, Account = account }).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
        var items = ImmutableArray.CreateBuilder<AdminStoreDto>();
        foreach (var row in rows.Take(pageSize)) items.Add(await ToDtoAsync(row.Store, row.Account, cancellationToken));
        return Result<MerchantPage<AdminStoreDto>>.Success(new(items.ToImmutable(), page, pageSize, rows.Length > pageSize));
    }
    public Task<Result<StoreDto>> UpdateAsync(Guid actorId, Guid id, AdminStoreUpdateWrite value, CancellationToken cancellationToken) => value is null
        ? Task.FromResult(Fail<StoreDto>(ErrorKind.Validation)) : stores.AdminUpdateAsync(actorId, id, new(value.RowVersion, value.Details), value.Reason, cancellationToken);
    public Task<Result<StoreDto>> ChangeLogoAsync(Guid actorId, Guid id, string rowVersion, Stream? content, string reason, CancellationToken cancellationToken) =>
        stores.AdminChangeLogoAsync(actorId, id, rowVersion, content, reason, cancellationToken);
    public Task<Result<byte[]>> ReadLogoAsync(Guid actorId, Guid id, CancellationToken cancellationToken) => stores.AdminReadLogoAsync(actorId, id, cancellationToken);
    public Task<Result<MerchantPage<BranchDto>>> BranchesAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken) => branches.AdminListAsync(actorId, storeId, page, pageSize, cancellationToken);
    public Task<Result<BranchDto>> UpdateBranchAsync(Guid actorId, Guid storeId, Guid id, AdminBranchUpdateWrite value, CancellationToken cancellationToken) => value is null
        ? Task.FromResult(Fail<BranchDto>(ErrorKind.Validation)) : branches.AdminUpdateAsync(actorId, storeId, id, new(value.RowVersion, value.Details), value.Reason, cancellationToken);
    public async Task<Result<StoreDto>> ModerateAsync(Guid actorId, Guid id, StoreAdministrationAction action, StoreModerationWrite value, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !Enum.IsDefined(action) || value is null || !MerchantReviewInput.Version(value.RowVersion) || !Store.AdminReason(value.Reason)) return Fail<StoreDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var result = await access.AdminStoreAsync(actorId, id, true, cancellationToken);
            if (!result.IsSuccess) return Result<StoreDto>.Failure(result.Error!);
            var store = result.Value;
            if (!store.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(value.RowVersion))) return Fail<StoreDto>(ErrorKind.Conflict);
            var before = State(store);
            var domainAction = action switch
            {
                StoreAdministrationAction.Hide => StoreModerationAction.Hide,
                StoreAdministrationAction.Publish => StoreModerationAction.Publish,
                StoreAdministrationAction.Suspend => StoreModerationAction.Suspend,
                StoreAdministrationAction.Resume => StoreModerationAction.Resume,
                _ => throw new InvalidOperationException("Invalid validated moderation action.")
            };
            if (!store.TryModerate(domainAction, value.Reason)) return Fail<StoreDto>(ErrorKind.Conflict);
            context.Add(AuditEntry.Record(actorId, "Store." + action, "Store", store.Id, value.Reason, clock.GetUtcNow(), before, State(store)));
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result<StoreDto>.Success(StoreService.ToDto(store));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Fail<StoreDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51003) { context.ChangeTracker.Clear(); return Fail<StoreDto>(ErrorKind.Conflict); }
    }
    public async Task<Result<MerchantPage<MerchantAuditDto>>> AuditAsync(Guid actorId, Guid id, bool branch, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!await IsAdminAsync(actorId, cancellationToken)) return Fail<MerchantPage<MerchantAuditDto>>(ErrorKind.Forbidden);
        if (id == Guid.Empty || !MerchantReviewInput.Page(page, pageSize)) return Fail<MerchantPage<MerchantAuditDto>>(ErrorKind.Validation);
        var kind = branch ? "MerchantBranch" : "Store";
        var rows = await context.Set<AuditEntry>().AsNoTracking().Where(value => value.TargetKind == kind && value.TargetId == id).OrderByDescending(value => value.OccurredAt).ThenBy(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(value => new MerchantAuditDto(value.Id, value.ActorId, value.Operation, value.TargetId, value.Reason, value.OccurredAt, value.PreviousState, value.CurrentState)).ToArrayAsync(cancellationToken);
        return Result<MerchantPage<MerchantAuditDto>>.Success(new(rows.Take(pageSize).ToImmutableArray(), page, pageSize, rows.Length > pageSize));
    }
    private async Task<AdminStoreDto> ToDtoAsync(Store store, MerchantAccount account, CancellationToken cancellationToken)
    {
        var identity = await identities.FindAsync(account.UserId, cancellationToken);
        return new(StoreService.ToDto(store), account.UserId, account.Status.ToString(), identity?.IsActive ?? false, identity?.EmailConfirmed ?? false);
    }
    private async Task<bool> IsAdminAsync(Guid id, CancellationToken cancellationToken) => await identities.FindAsync(id, cancellationToken) is { AccountType: "Admin", EmailConfirmed: true, IsActive: true };
    private static string State(Store store) => $"Hidden={store.IsHidden};Suspended={store.IsSuspended}";
    private static Result<T> Fail<T>(ErrorKind kind) => Result<T>.Failure(new(kind));
}
