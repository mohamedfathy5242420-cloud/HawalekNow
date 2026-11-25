using HawalekNow.Application.Modules.Search;
using HawalekNow.Application.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public static class FoundationSearchPorts
{
    public static IServiceCollection AddFoundationSearchPorts(this IServiceCollection services) => services.AddScoped<ISynonymAdministration, Unused>();
    private sealed class Unused : ISynonymAdministration
    {
        public Task<Result<SynonymDto>> SaveAsync(Guid? id, SynonymWrite value, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result> DeleteAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
        public Task<Result<SynonymPage>> ListAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new InvalidOperationException("An isolated foundation test invoked a business port.");
    }
}
