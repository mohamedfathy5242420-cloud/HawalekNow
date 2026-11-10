using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using HawalekNow.Infrastructure.Shared.Messaging;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class IdentityConfigurationValidation(IdentifierDigests digests, OtpProtection otp, PayloadProtection payloads, IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = digests; // Constructor validation enforces a stable key; no generated fallback at application startup.
        _ = otp; _ = payloads;
        try
        {
            var connection = new SqlConnectionStringBuilder(configuration.GetConnectionString("Application") ?? "");
            if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog) ||
                new[] { "master", "model", "msdb", "tempdb" }.Contains(connection.InitialCatalog, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException();
        }
        catch (ArgumentException) { throw new InvalidOperationException("Configure ConnectionStrings:Application with a specific application database."); }
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
