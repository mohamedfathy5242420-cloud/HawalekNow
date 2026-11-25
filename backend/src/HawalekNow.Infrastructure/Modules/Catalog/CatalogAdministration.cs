using HawalekNow.Application.Shared.Caching;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class CatalogAdministration(HawalekDbContext context, CatalogTreeLock treeLock, IReferenceOptionsCache cache) : ICatalogAdministration
{
    public async Task<Result<ActivityAdminDto>> SaveActivityAsync(Guid? id, ActivityWrite value, CancellationToken cancellationToken)
    {
        if (value is null || !CatalogInput.Name(value.NameAr) || !CatalogInput.Name(value.NameEn) || id == Guid.Empty || (id is not null && !CatalogInput.Version(value.RowVersion)))
            return Failure<ActivityAdminDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await treeLock.AcquireAsync(cancellationToken);
            Activity activity;
            if (id is null) { activity = Activity.Create(value.NameAr, value.NameEn, value.IsActive); context.Add(activity); }
            else
            {
                var found = await context.Set<Activity>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (found is null) return Failure<ActivityAdminDto>(ErrorKind.NotFound);
                await context.Entry(found).ReloadAsync(cancellationToken);
                if (!Matches(found.RowVersion, value.RowVersion)) return Failure<ActivityAdminDto>(ErrorKind.Conflict);
                activity = found; activity.Rename(value.NameAr, value.NameEn); activity.SetActive(value.IsActive);
            }
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); cache.Invalidate(ReferenceDataFamily.Catalog);
            return Result<ActivityAdminDto>.Success(ToDto(activity));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Failure<ActivityAdminDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51010) { context.ChangeTracker.Clear(); return Failure<ActivityAdminDto>(ErrorKind.Conflict); }
    }
    public async Task<Result<CategoryAdminDto>> SaveCategoryAsync(Guid? id, CategoryWrite value, CancellationToken cancellationToken)
    {
        if (value is null || !CatalogInput.Name(value.NameAr) || !CatalogInput.Name(value.NameEn) || value.ActivityId == Guid.Empty || value.ParentId == Guid.Empty || id == Guid.Empty || (id is not null && !CatalogInput.Version(value.RowVersion)))
            return Failure<CategoryAdminDto>(ErrorKind.Validation);
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await treeLock.AcquireAsync(cancellationToken);
            if (!await context.Set<Activity>().AnyAsync(item => item.Id == value.ActivityId, cancellationToken)) return Failure<CategoryAdminDto>(ErrorKind.Validation);
            Category category;
            if (id is null) category = Category.Create(value.ActivityId, null, value.NameAr, value.NameEn, value.IsActive);
            else
            {
                var found = await context.Set<Category>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
                if (found is null) return Failure<CategoryAdminDto>(ErrorKind.NotFound);
                await context.Entry(found).ReloadAsync(cancellationToken);
                if (!Matches(found.RowVersion, value.RowVersion)) return Failure<CategoryAdminDto>(ErrorKind.Conflict);
                if (found.ActivityId != value.ActivityId) return Failure<CategoryAdminDto>(ErrorKind.Validation);
                category = found;
            }
            if (!await ParentAllowedAsync(category.Id, value.ActivityId, value.ParentId, cancellationToken)) return Failure<CategoryAdminDto>(ErrorKind.Conflict);
            category.SetParent(value.ParentId); category.Rename(value.NameAr, value.NameEn); category.SetActive(value.IsActive);
            if (id is null) context.Add(category);
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); cache.Invalidate(ReferenceDataFamily.Catalog);
            return Result<CategoryAdminDto>.Success(ToDto(category));
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Failure<CategoryAdminDto>(ErrorKind.Conflict); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51010) { context.ChangeTracker.Clear(); return Failure<CategoryAdminDto>(ErrorKind.Conflict); }
    }
    public async Task<Result> DeleteActivityAsync(Guid id, string rowVersion, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !CatalogInput.Version(rowVersion)) return Result.Failure(new(ErrorKind.Validation));
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await treeLock.AcquireAsync(cancellationToken);
            var activity = await context.Set<Activity>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (activity is null) return Result.Failure(new(ErrorKind.NotFound));
            await context.Entry(activity).ReloadAsync(cancellationToken);
            if (!Matches(activity.RowVersion, rowVersion) || await context.Set<Category>().AnyAsync(item => item.ActivityId == id, cancellationToken)) return Result.Failure(new(ErrorKind.Conflict));
            context.Remove(activity); await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); cache.Invalidate(ReferenceDataFamily.Catalog); return Result.Success();
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51010) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
    }
    public async Task<Result> DeleteCategoryAsync(Guid id, string rowVersion, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || !CatalogInput.Version(rowVersion)) return Result.Failure(new(ErrorKind.Validation));
        try
        {
            await using var transaction = await context.BeginAsync(cancellationToken); await treeLock.AcquireAsync(cancellationToken);
            var category = await context.Set<Category>().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (category is null) return Result.Failure(new(ErrorKind.NotFound));
            await context.Entry(category).ReloadAsync(cancellationToken);
            if (!Matches(category.RowVersion, rowVersion) || await context.Set<Category>().AnyAsync(item => item.ParentId == id, cancellationToken)) return Result.Failure(new(ErrorKind.Conflict));
            context.Remove(category); await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); cache.Invalidate(ReferenceDataFamily.Catalog); return Result.Success();
        }
        catch (DbUpdateException) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
        catch (SqlException error) when (error.Number is 1205 or 1222 or 51010) { context.ChangeTracker.Clear(); return Result.Failure(new(ErrorKind.Conflict)); }
    }
    private async Task<bool> ParentAllowedAsync(Guid id, Guid activityId, Guid? parentId, CancellationToken cancellationToken)
    {
        var path = new CategoryPath(id, activityId);
        while (parentId is { } current)
        {
            var parent = await context.Set<Category>().AsNoTracking().Where(item => item.Id == current)
                .Select(item => new { item.ActivityId, item.ParentId }).SingleOrDefaultAsync(cancellationToken);
            if (parent is null || !path.TryAppend(current, parent.ActivityId)) return false;
            parentId = parent.ParentId;
        }
        return true;
    }
    private static bool Matches(byte[] actual, string? expected) => expected is not null && actual.AsSpan().SequenceEqual(Convert.FromBase64String(expected));
    private static Result<T> Failure<T>(ErrorKind kind) => Result<T>.Failure(new(kind));
    internal static ActivityAdminDto ToDto(Activity activity) => new(activity.Id, activity.NameAr, activity.NameEn, activity.IsActive, Convert.ToBase64String(activity.RowVersion));
    internal static CategoryAdminDto ToDto(Category category) => new(category.Id, category.ActivityId, category.ParentId, category.NameAr, category.NameEn, category.IsActive, Convert.ToBase64String(category.RowVersion));
}
