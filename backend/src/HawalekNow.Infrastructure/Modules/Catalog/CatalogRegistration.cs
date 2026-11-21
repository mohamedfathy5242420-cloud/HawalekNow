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
        services.AddScoped<ICatalogQueries, CatalogQueries>();
        services.AddScoped<IInitialActivities, InitialActivities>();
        return services;
    }
}
