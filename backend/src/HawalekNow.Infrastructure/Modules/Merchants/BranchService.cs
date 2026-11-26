using System.Collections.Immutable;
using HawalekNow.Application.Modules.Locations.Contracts;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class BranchService(HawalekDbContext context, MerchantStoreAccess access, IAdministrativeDirectory locations, TimeProvider clock) : IBranchService
{
    public async Task<Result<BranchDto>> CreateAsync(Guid actorId, Guid storeId, BranchWrite value, CancellationToken cancellationToken)
    {
        if (storeId == Guid.Empty || !BranchInput.Normalize(value, out var details)) return Fail<BranchDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var owned = await access.OwnStoreAsync(actorId, storeId, true, cancellationToken);
            if (!owned.IsSuccess) return Result<BranchDto>.Failure(owned.Error!);
            if (!await locations.CanSelectInsideTransactionAsync(value.GovernorateId, value.CityId, value.AreaId, cancellationToken)) return Fail<BranchDto>(ErrorKind.Validation);
            var branch = MerchantBranch.Create(storeId, details!); context.Add(branch); Audit(actorId, "Branch.Create", branch.Id);
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result<BranchDto>.Success(ToDto(branch, owned.Value));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Fail<BranchDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (Conflict(error)) { context.ChangeTracker.Clear(); return Fail<BranchDto>(ErrorKind.Conflict); }
    }
    public async Task<Result<BranchDto>> UpdateAsync(Guid actorId, Guid storeId, Guid id, BranchUpdateWrite value, CancellationToken cancellationToken)
    {
        if (storeId == Guid.Empty || id == Guid.Empty || value is null || !MerchantReviewInput.Version(value.RowVersion) || !BranchInput.Normalize(value.Details, out var details)) return Fail<BranchDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var owned = await access.OwnStoreAsync(actorId, storeId, true, cancellationToken);
            if (!owned.IsSuccess) return Result<BranchDto>.Failure(owned.Error!);
            var branch = await context.Set<MerchantBranch>().SingleOrDefaultAsync(item => item.Id == id && item.StoreId == storeId, cancellationToken);
            if (branch is null) return Fail<BranchDto>(ErrorKind.NotFound);
            await context.Entry(branch).ReloadAsync(cancellationToken);
            if (!branch.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(value.RowVersion))) return Fail<BranchDto>(ErrorKind.Conflict);
            if ((branch.AreaId != details!.AreaId || branch.CityId != details.CityId || branch.GovernorateId != details.GovernorateId) &&
                !await locations.CanSelectInsideTransactionAsync(details.GovernorateId, details.CityId, details.AreaId, cancellationToken)) return Fail<BranchDto>(ErrorKind.Validation);
            branch.Update(details); Audit(actorId, "Branch.Edit", branch.Id);
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return Result<BranchDto>.Success(ToDto(branch, owned.Value));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Fail<BranchDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (Conflict(error)) { context.ChangeTracker.Clear(); return Fail<BranchDto>(ErrorKind.Conflict); }
    }
    public async Task<Result<MerchantPage<BranchDto>>> OwnListAsync(Guid actorId, Guid storeId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!MerchantReviewInput.Page(page, pageSize)) return Fail<MerchantPage<BranchDto>>(ErrorKind.Validation);
        var owned = await access.OwnStoreAsync(actorId, storeId, false, cancellationToken);
        if (!owned.IsSuccess) return Result<MerchantPage<BranchDto>>.Failure(owned.Error!);
        var rows = await Rows(storeId, page, pageSize, cancellationToken);
        return Result<MerchantPage<BranchDto>>.Success(new(rows.Take(pageSize).Select(value => ToDto(value, owned.Value)).ToImmutableArray(), page, pageSize, rows.Count > pageSize));
    }
    public async Task<Result<MerchantPage<PublicBranchDto>>> PublicListAsync(Guid storeId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!MerchantReviewInput.Page(page, pageSize)) return Fail<MerchantPage<PublicBranchDto>>(ErrorKind.Validation);
        var store = await context.Set<Store>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == storeId, cancellationToken);
        if (store is null || !await access.PublicAsync(store, cancellationToken)) return Fail<MerchantPage<PublicBranchDto>>(ErrorKind.NotFound);
        var rows = await Rows(storeId, page, pageSize, cancellationToken);
        return Result<MerchantPage<PublicBranchDto>>.Success(new(rows.Take(pageSize).Select(value => ToPublicDto(value, store)).ToImmutableArray(), page, pageSize, rows.Count > pageSize));
    }
    public async Task<Result<PublicBranchDto>> PublicAsync(Guid id, CancellationToken cancellationToken)
    {
        var branch = await context.Set<MerchantBranch>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (branch is null) return Fail<PublicBranchDto>(ErrorKind.NotFound);
        var store = await context.Set<Store>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == branch.StoreId, cancellationToken);
        return store is null || !await access.PublicAsync(store, cancellationToken) ? Fail<PublicBranchDto>(ErrorKind.NotFound) : Result<PublicBranchDto>.Success(ToPublicDto(branch, store));
    }
    private Task<List<MerchantBranch>> Rows(Guid storeId, int page, int pageSize, CancellationToken cancellationToken) => context.Set<MerchantBranch>().AsNoTracking()
        .Where(value => value.StoreId == storeId).OrderBy(value => value.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync(cancellationToken);
    private void Audit(Guid actorId, string operation, Guid id) => context.Add(AuditEntry.Record(actorId, operation, "MerchantBranch", id, null, clock.GetUtcNow()));
    private static bool Conflict(SqlException error) => error.Number is 1205 or 1222 or 51003 or 51011;
    private static Result<T> Fail<T>(ErrorKind kind) => Result<T>.Failure(new(kind));
    public static BranchDto ToDto(MerchantBranch branch, Store store) => new(branch.Id, store.Id, branch.Kind.ToString(), branch.Name, branch.Address, branch.GovernorateId,
        branch.CityId, branch.AreaId, branch.Position.Latitude, branch.Position.Longitude, branch.Phone, branch.WhatsApp, branch.Phone ?? store.Phone, branch.WhatsApp ?? store.WhatsApp,
        branch.PublicFixedAddressConfirmed, Convert.ToBase64String(branch.RowVersion));
    public static PublicBranchDto ToPublicDto(MerchantBranch branch, Store store) => new(branch.Id, store.Id, branch.Kind.ToString(), branch.Name, branch.Address, branch.GovernorateId,
        branch.CityId, branch.AreaId, branch.Position.Latitude, branch.Position.Longitude, branch.Phone ?? store.Phone, branch.WhatsApp ?? store.WhatsApp);
}
