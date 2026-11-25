using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// P01 pipeline/logger/error hosts validate DI without SQL or business endpoints.
// These ports must never be invoked; Catalog HTTP and concurrency tests use the real implementations.
public static class FoundationCatalogPorts
{
    public static IServiceCollection AddFoundationCatalogPorts(this IServiceCollection services) =>
        services.AddScoped<ICatalogAdministration, UnusedAdministration>().AddScoped<ICatalogQueries, UnusedQueries>();
    private sealed class UnusedAdministration : ICatalogAdministration
    {
        public Task<Result<ActivityAdminDto>> SaveActivityAsync(Guid? id, ActivityWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<CategoryAdminDto>> SaveCategoryAsync(Guid? id, CategoryWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result> DeleteActivityAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => throw Unused();
        public Task<Result> DeleteCategoryAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => throw Unused();
    }
    private sealed class UnusedQueries : ICatalogQueries
    {
        public Task<Result<CatalogPage<CatalogOption>>> ActivitiesAsync(string language, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<CatalogPage<CatalogOption>>> CategoriesAsync(Guid activityId, Guid? parentId, string language, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<CatalogPage<ActivityAdminDto>>> AdminActivitiesAsync(int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<CatalogPage<CategoryAdminDto>>> AdminCategoriesAsync(Guid activityId, Guid? parentId, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
    }
    private static InvalidOperationException Unused() => new("An isolated foundation test invoked a business port.");
}
