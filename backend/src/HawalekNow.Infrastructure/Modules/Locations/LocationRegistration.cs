using HawalekNow.Infrastructure.Shared.Caching;
using HawalekNow.Application.Modules.Locations;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.Infrastructure.Modules.Locations;

public static class LocationRegistration
{
    public static IServiceCollection AddLocationsReferenceData(this IServiceCollection services)
    {
        services.AddScoped<LocationStateLock>(); services.AddScoped<ILocationAdministration, LocationAdministration>(); services.AddScoped<LocationQueries>(); services.AddScoped<ILocationQueries, CachedLocationQueries>(); services.AddReferenceOptionsCache();
        services.AddSingleton<IReferenceSeedSource, EmbeddedReferenceSeedSource>(); services.AddScoped<IReferenceDataSeeder, ReferenceDataSeeder>();
        return services;
    }
}
