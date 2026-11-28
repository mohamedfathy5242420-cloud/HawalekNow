using HawalekNow.Application.Modules.Catalog.Contracts;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Application.Shared.Storage;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed class StoreService(HawalekDbContext context, MerchantStoreAccess access, IActivityDirectory activities, IFileStorage files,
    StoredFileLock fileLock, LogoDeletionIntents deletionIntents, LogoCleanup cleanup, TimeProvider clock) : IStoreService
{
    public async Task<Result<StoreDto>> CreateAsync(Guid actorId, StoreCreateWrite value, CancellationToken cancellationToken)
    {
        if (value is null || value.ActivityId == Guid.Empty || !StoreInput.Normalize(value.Details, out var details)) return Fail<StoreDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var owner = await access.OwnerAsync(actorId, true, cancellationToken);
            if (!owner.IsSuccess) return Result<StoreDto>.Failure(owner.Error!);
            if (await context.Set<Store>().AnyAsync(item => item.MerchantAccountId == owner.Value.Id, cancellationToken)) return Fail<StoreDto>(ErrorKind.Conflict);
            if (!await activities.CanSelectInsideTransactionAsync(value.ActivityId, cancellationToken)) return Fail<StoreDto>(ErrorKind.Validation);
            var store = Store.Create(owner.Value.Id, value.ActivityId, details!); context.Add(store);
            Audit(actorId, "Store.Create", store.Id); await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Result<StoreDto>.Success(ToDto(store));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Fail<StoreDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (Conflict(error)) { context.ChangeTracker.Clear(); return Fail<StoreDto>(ErrorKind.Conflict); }
    }
    public Task<Result<StoreDto>> UpdateAsync(Guid actorId, Guid id, StoreUpdateWrite value, CancellationToken cancellationToken) => UpdateCoreAsync(actorId, id, value, false, null, cancellationToken);
    internal Task<Result<StoreDto>> AdminUpdateAsync(Guid actorId, Guid id, StoreUpdateWrite value, string reason, CancellationToken cancellationToken) => UpdateCoreAsync(actorId, id, value, true, reason, cancellationToken);
    private async Task<Result<StoreDto>> UpdateCoreAsync(Guid actorId, Guid id, StoreUpdateWrite value, bool admin, string? reason, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || value is null || !MerchantReviewInput.Version(value.RowVersion) || !StoreInput.Normalize(value.Details, out var details) || admin && !Store.AdminReason(reason)) return Fail<StoreDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var owned = admin ? await access.AdminStoreAsync(actorId, id, true, cancellationToken) : await access.OwnStoreAsync(actorId, id, true, cancellationToken);
            if (!owned.IsSuccess) return Result<StoreDto>.Failure(owned.Error!);
            var store = owned.Value;
            if (!Matches(store, value.RowVersion)) return Fail<StoreDto>(ErrorKind.Conflict);
            store.Update(details!); Audit(actorId, admin ? "Store.AdminEdit" : "Store.Edit", store.Id, reason);
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Result<StoreDto>.Success(ToDto(store));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Fail<StoreDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (Conflict(error)) { context.ChangeTracker.Clear(); return Fail<StoreDto>(ErrorKind.Conflict); }
    }
    public async Task<Result<StoreDto>> OwnAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var owner = await access.OwnerAsync(actorId, false, cancellationToken);
        if (!owner.IsSuccess) return Result<StoreDto>.Failure(owner.Error!);
        var store = await context.Set<Store>().AsNoTracking().SingleOrDefaultAsync(value => value.MerchantAccountId == owner.Value.Id, cancellationToken);
        return store is null ? Fail<StoreDto>(ErrorKind.NotFound) : Result<StoreDto>.Success(ToDto(store));
    }
    public async Task<Result<PublicStoreDto>> PublicAsync(Guid id, CancellationToken cancellationToken)
    {
        var store = await context.Set<Store>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return store is null || !await access.PublicAsync(store, cancellationToken) ? Fail<PublicStoreDto>(ErrorKind.NotFound) : Result<PublicStoreDto>.Success(ToPublicDto(store));
    }
    public Task<Result<StoreDto>> ChangeLogoAsync(Guid actorId, Guid id, string rowVersion, Stream? content, CancellationToken cancellationToken) => ChangeLogoCoreAsync(actorId, id, rowVersion, content, false, null, cancellationToken);
    internal Task<Result<StoreDto>> AdminChangeLogoAsync(Guid actorId, Guid id, string rowVersion, Stream? content, string reason, CancellationToken cancellationToken) => ChangeLogoCoreAsync(actorId, id, rowVersion, content, true, reason, cancellationToken);
    private async Task<Result<StoreDto>> ChangeLogoCoreAsync(Guid actorId, Guid id, string rowVersion, Stream? content, bool admin, string? reason, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !MerchantReviewInput.Version(rowVersion) || content is not null && !content.CanRead || admin && !Store.AdminReason(reason)) return Fail<StoreDto>(ErrorKind.Validation);
        string? newKey = null;
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken);
            var owned = admin ? await access.AdminStoreAsync(actorId, id, true, cancellationToken) : await access.OwnStoreAsync(actorId, id, true, cancellationToken);
            if (!owned.IsSuccess) return Result<StoreDto>.Failure(owned.Error!);
            var store = owned.Value;
            if (!Matches(store, rowVersion)) return Fail<StoreDto>(ErrorKind.Conflict);
            if (content is null && store.LogoKey is null) return Result<StoreDto>.Success(ToDto(store));
            if (content is not null)
            {
                newKey = Guid.CreateVersion7().ToString("N");
                await fileLock.AcquireAsync(newKey, cancellationToken);
                _ = await files.WriteImageAsync(newKey, content, cancellationToken);
            }
            var previousKey = store.LogoKey; store.SetLogo(newKey);
            if (previousKey is not null)
            {
                // Cleanup belongs to the merchant's commercial data; the Admin remains the Audit actor.
                var ownerId = admin ? await context.Set<MerchantAccount>().AsNoTracking().Where(value => value.Id == store.MerchantAccountId).Select(value => value.UserId).SingleAsync(cancellationToken) : actorId;
                deletionIntents.Add(previousKey, ownerId);
            }
            var operation = content is null ? "RemoveLogo" : "ChangeLogo";
            Audit(actorId, "Store." + (admin ? "Admin" : "") + operation, id, reason);
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Result<StoreDto>.Success(ToDto(store));
        }
        catch (InvalidImageException) { await cleanup.TryCompensateAsync(newKey); return Fail<StoreDto>(ErrorKind.Validation); }
        catch (DbUpdateException) { await cleanup.TryCompensateAsync(newKey); return Fail<StoreDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (Conflict(error)) { await cleanup.TryCompensateAsync(newKey); return Fail<StoreDto>(ErrorKind.Conflict); }
        catch (IOException) { await cleanup.TryCompensateAsync(newKey); return Fail<StoreDto>(ErrorKind.Unavailable); }
        catch (UnauthorizedAccessException) { await cleanup.TryCompensateAsync(newKey); return Fail<StoreDto>(ErrorKind.Unavailable); }
        catch { await cleanup.TryCompensateAsync(newKey); throw; }
    }
    public Task<Result<byte[]>> ReadLogoAsync(Guid? actorId, Guid id, CancellationToken cancellationToken) => ReadLogoCoreAsync(actorId, id, false, cancellationToken);
    internal Task<Result<byte[]>> AdminReadLogoAsync(Guid actorId, Guid id, CancellationToken cancellationToken) => ReadLogoCoreAsync(actorId, id, true, cancellationToken);
    private async Task<Result<byte[]>> ReadLogoCoreAsync(Guid? actorId, Guid id, bool admin, CancellationToken cancellationToken)
    {
        // A concurrent replacement may remove the previous object after our first reference read.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            Store? store;
            if (actorId is not null)
            {
                var owned = admin ? await access.AdminStoreAsync(actorId.Value, id, false, cancellationToken) : await access.OwnStoreAsync(actorId.Value, id, false, cancellationToken);
                if (!owned.IsSuccess) return Result<byte[]>.Failure(owned.Error!);
                store = owned.Value;
            }
            else
            {
                store = await context.Set<Store>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
                if (store is null || !await access.PublicAsync(store, cancellationToken)) return Fail<byte[]>(ErrorKind.NotFound);
            }
            if (store.LogoKey is null) return Fail<byte[]>(ErrorKind.NotFound);
            try
            {
                var bytes = await files.ReadImageAsync(store.LogoKey, cancellationToken);
                if (bytes is not null) return Result<byte[]>.Success(bytes);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return Fail<byte[]>(ErrorKind.Unavailable);
    }
    private void Audit(Guid actorId, string operation, Guid id, string? reason = null) => context.Add(AuditEntry.Record(actorId, operation, "Store", id, reason, clock.GetUtcNow()));
    private static bool Conflict(SqlException error) => error.Number is 1205 or 1222 or 51003 or 51010 or 51021;
    private static bool Matches(Store store, string expected) => store.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(expected));
    private static Result<T> Fail<T>(ErrorKind kind) => Result<T>.Failure(new(kind));
    public static StoreDto ToDto(Store store) => new(store.Id, store.MerchantAccountId, store.ActivityId, store.Name, store.Phone, store.WhatsApp,
        store.Facebook, store.Instagram, store.TikTok, store.LogoKey is not null, Convert.ToBase64String(store.RowVersion), store.IsHidden, store.IsSuspended, store.VisibilityReason, store.SuspensionReason, store.CanOwnerEdit);
    public static PublicStoreDto ToPublicDto(Store store) => new(store.Id, store.ActivityId, store.Name, store.Phone, store.WhatsApp, store.Facebook, store.Instagram, store.TikTok, store.LogoKey is not null);
}
