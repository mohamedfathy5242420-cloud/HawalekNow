using HawalekNow.Application.Modules.Locations;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.Infrastructure.Modules.Locations;

public static class LocationRegistration
{
    public static IServiceCollection AddLocationsReferenceData(this IServiceCollection services)
    {
        services.AddScoped<LocationStateLock>(); services.AddScoped<ILocationAdministration, LocationAdministration>(); services.AddScoped<ILocationQueries, LocationQueries>();
        services.AddSingleton<IReferenceSeedSource, EmbeddedReferenceSeedSource>(); services.AddScoped<IReferenceDataSeeder, ReferenceDataSeeder>();
        return services;
    }
}
