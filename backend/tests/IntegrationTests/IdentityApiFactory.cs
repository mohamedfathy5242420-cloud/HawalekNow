using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.IntegrationTests;

public sealed class IdentityApiFactory(SqlDatabase database, TimeProvider? clock = null, Action<IServiceCollection>? configure = null,
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(TestIdentityConfiguration.Values(database.ConnectionString, database.IdentifierKey));
            if (settings is not null) configuration.AddInMemoryCollection(settings);
        });
        builder.ConfigureTestServices(services =>
        {
            if (clock is not null) { services.RemoveAll<TimeProvider>(); services.AddSingleton(clock); }
            configure?.Invoke(services);
        });
    }
    public HttpClient CreateHttpsClient() => CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
}
