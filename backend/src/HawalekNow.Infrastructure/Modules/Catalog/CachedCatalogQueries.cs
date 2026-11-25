using System.Collections.Immutable;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Shared.Caching;
using HawalekNow.Application.Shared.Results;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public sealed class CachedCatalogQueries(CatalogQueries queries, IReferenceOptionsCache cache) : ICatalogQueries
{
    public Task<Result<CatalogPage<CatalogOption>>> ActivitiesAsync(string language, int page, int pageSize, CancellationToken cancellationToken) =>
        ReadAsync(new(ReferenceDataFamily.Catalog, "activities", language, null, null, page, pageSize), ct => queries.ActivitiesAsync(language, page, pageSize, ct), cancellationToken);
    public Task<Result<CatalogPage<CatalogOption>>> CategoriesAsync(Guid activityId, Guid? parentId, string language, int page, int pageSize, CancellationToken cancellationToken) =>
        ReadAsync(new(ReferenceDataFamily.Catalog, "categories", language, activityId, parentId, page, pageSize), ct => queries.CategoriesAsync(activityId, parentId, language, page, pageSize, ct), cancellationToken);
    private async Task<Result<CatalogPage<CatalogOption>>> ReadAsync(ReferenceOptionsKey key, Func<CancellationToken, Task<Result<CatalogPage<CatalogOption>>>> load, CancellationToken ct)
    {
        var result = await cache.GetAsync(key, async token =>
        {
            var page = await load(token);
            return page.IsSuccess ? Result<ReferencePageSnapshot>.Success(new(page.Value.Items.Select(item => new ReferenceOptionSnapshot(item.Id, item.Name)).ToImmutableArray(), page.Value.Page, page.Value.PageSize, page.Value.HasMore)) : Result<ReferencePageSnapshot>.Failure(page.Error!);
        }, ct);
        return result.IsSuccess ? Result<CatalogPage<CatalogOption>>.Success(new(result.Value.Items.Select(item => new CatalogOption(item.Id, item.Name)).ToImmutableArray(), result.Value.Page, result.Value.PageSize, result.Value.HasMore)) : Result<CatalogPage<CatalogOption>>.Failure(result.Error!);
    }
    public Task<Result<CatalogPage<ActivityAdminDto>>> AdminActivitiesAsync(int page, int pageSize, CancellationToken cancellationToken) => queries.AdminActivitiesAsync(page, pageSize, cancellationToken);
    public Task<Result<CatalogPage<CategoryAdminDto>>> AdminCategoriesAsync(Guid activityId, Guid? parentId, int page, int pageSize, CancellationToken cancellationToken) => queries.AdminCategoriesAsync(activityId, parentId, page, pageSize, cancellationToken);
}
