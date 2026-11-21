using System.Collections.Immutable;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class CatalogQueries(HawalekDbContext context) : ICatalogQueries
{
    public async Task<Result<CatalogPage<CatalogOption>>> ActivitiesAsync(string language, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!CatalogInput.Language(language) || !CatalogInput.Page(page, pageSize)) return Invalid<CatalogOption>();
        var rows = await context.Set<Activity>().AsNoTracking().Where(value => value.IsActive).OrderBy(value => value.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1).Select(value => new CatalogOption(value.Id, language == "ar" ? value.NameAr : value.NameEn)).ToArrayAsync(cancellationToken);
        return Page(rows, page, pageSize);
    }
    public async Task<Result<CatalogPage<CatalogOption>>> CategoriesAsync(Guid activityId, Guid? parentId, string language, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (activityId == Guid.Empty || parentId == Guid.Empty || !CatalogInput.Language(language) || !CatalogInput.Page(page, pageSize)) return Invalid<CatalogOption>();
        // A shared transaction lock makes the ancestor checks and child query one read relative to admin commits.
        await using var transaction = await context.BeginAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource={"catalog-reference-tree"}, @LockMode=N'Shared', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51010, N'Reference tree lock unavailable.', 1;
            """, cancellationToken);
        if (!await VisiblePathAsync(activityId, parentId, cancellationToken)) return Page(Array.Empty<CatalogOption>(), page, pageSize);
        var rows = await context.Set<Category>().AsNoTracking().Where(value => value.ActivityId == activityId && value.ParentId == parentId && value.IsActive)
            .OrderBy(value => value.Id).Skip((page - 1) * pageSize).Take(pageSize + 1)
            .Select(value => new CatalogOption(value.Id, language == "ar" ? value.NameAr : value.NameEn)).ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken); return Page(rows, page, pageSize);
    }
    public async Task<Result<CatalogPage<ActivityAdminDto>>> AdminActivitiesAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (!CatalogInput.Page(page, pageSize)) return Invalid<ActivityAdminDto>();
        var rows = await context.Set<Activity>().AsNoTracking().OrderBy(value => value.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
        return Page(rows.Select(CatalogAdministration.ToDto).ToArray(), page, pageSize);
    }
    public async Task<Result<CatalogPage<CategoryAdminDto>>> AdminCategoriesAsync(Guid activityId, Guid? parentId, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (activityId == Guid.Empty || parentId == Guid.Empty || !CatalogInput.Page(page, pageSize)) return Invalid<CategoryAdminDto>();
        var rows = await context.Set<Category>().AsNoTracking().Where(value => value.ActivityId == activityId && value.ParentId == parentId)
            .OrderBy(value => value.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToArrayAsync(cancellationToken);
        return Page(rows.Select(CatalogAdministration.ToDto).ToArray(), page, pageSize);
    }
    private async Task<bool> VisiblePathAsync(Guid activityId, Guid? parentId, CancellationToken cancellationToken)
    {
        if (!await context.Set<Activity>().AnyAsync(value => value.Id == activityId && value.IsActive, cancellationToken)) return false;
        var seen = new HashSet<Guid>();
        while (parentId is { } current)
        {
            if (!seen.Add(current)) return false;
            var row = await context.Set<Category>().AsNoTracking().Where(value => value.Id == current)
                .Select(value => new { value.ActivityId, value.ParentId, value.IsActive }).SingleOrDefaultAsync(cancellationToken);
            if (row is null || row.ActivityId != activityId || !row.IsActive) return false;
            parentId = row.ParentId;
        }
        return true;
    }
    private static Result<CatalogPage<T>> Invalid<T>() => Result<CatalogPage<T>>.Failure(new(ErrorKind.Validation));
    private static Result<CatalogPage<T>> Page<T>(T[] rows, int page, int pageSize) => Result<CatalogPage<T>>.Success(new(rows.Take(pageSize).ToImmutableArray(), page, pageSize, rows.Length > pageSize));
}
