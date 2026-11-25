using HawalekNow.Infrastructure.Shared.Caching;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Modules.Catalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.Infrastructure.Modules.Catalog;

public static class CatalogRegistration
{
    public static IServiceCollection AddCatalogReferenceData(this IServiceCollection services)
    {
        services.AddScoped<CatalogTreeLock>();
        services.AddScoped<ICatalogAdministration, CatalogAdministration>();
        services.AddScoped<CatalogQueries>(); services.AddScoped<ICatalogQueries, CachedCatalogQueries>(); services.AddReferenceOptionsCache();
        services.AddScoped<IInitialActivities, InitialActivities>();
        return services;
    }
}
