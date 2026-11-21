using HawalekNow.Application.Modules.Locations;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public static class FoundationLocationPorts
{
    public static IServiceCollection AddFoundationLocationPorts(this IServiceCollection services) => services.AddScoped<ILocationAdministration, UnusedAdministration>().AddScoped<ILocationQueries, UnusedQueries>();
    private sealed class UnusedAdministration : ILocationAdministration
    {
        public Task<Result<LocationAdminDto>> SaveAsync(LocationKind kind, Guid? id, LocationWrite value, CancellationToken cancellationToken) => throw Unused();
        public Task<Result> DeleteAsync(LocationKind kind, Guid id, string rowVersion, CancellationToken cancellationToken) => throw Unused();
    }
    private sealed class UnusedQueries : ILocationQueries
    {
        public Task<Result<LocationPage<LocationOption>>> OptionsAsync(LocationKind kind, Guid? governorateId, Guid? cityId, string language, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
        public Task<Result<LocationPage<LocationAdminDto>>> AdminAsync(LocationKind kind, Guid? governorateId, Guid? cityId, int page, int pageSize, CancellationToken cancellationToken) => throw Unused();
    }
    private static InvalidOperationException Unused() => new("An isolated foundation test invoked a business port.");
}
