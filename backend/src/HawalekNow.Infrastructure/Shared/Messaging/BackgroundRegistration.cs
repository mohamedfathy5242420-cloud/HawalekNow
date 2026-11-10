using Hangfire;
using Hangfire.AspNetCore;
using Hangfire.Logging;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HawalekNow.Infrastructure.Shared.Messaging;

public static class BackgroundRegistration
{
    public static IServiceCollection AddBackgroundProcessing(this IServiceCollection services)
    {
        // Deliberately closed: no global Serilog logger or general Hangfire/SQL diagnostics can expose metadata.
        LogProvider.SetCurrentLogProvider(new ClosedHangfireLogs());
        services.AddSingleton<PayloadProtection>();
        services.AddScoped<OutboxWriter>(); services.AddScoped<OutboxPump>();
        services.AddSingleton<JobStorage>(provider => new SqlServerStorage(
            provider.GetRequiredService<IConfiguration>().GetConnectionString("Application") ?? throw new InvalidOperationException("Configure the application database."),
            new SqlServerStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromSeconds(1),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                UseRecommendedIsolationLevel = true
            }));
        services.AddSingleton<IBackgroundJobClient>(provider => new BackgroundJobClient(provider.GetRequiredService<JobStorage>()));
        services.AddSingleton<IRecurringJobManager>(provider => new RecurringJobManager(provider.GetRequiredService<JobStorage>()));
        services.AddHostedService<HangfireWorker>();
        return services;
    }
    private sealed class ClosedHangfireLogs : ILogProvider, ILog
    {
        public ILog GetLogger(string name) => this;
        public bool Log(LogLevel logLevel, Func<string> messageFunc, Exception? exception = null) => false;
    }
}

public sealed class HangfireWorker(IServiceProvider provider, IConfiguration configuration) : IHostedService, IDisposable
{
    private BackgroundJobServer? server;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Background:Enabled", true)) return Task.CompletedTask;
        var storage = provider.GetRequiredService<JobStorage>();
        server = new BackgroundJobServer(new BackgroundJobServerOptions
        {
            WorkerCount = 1,
            Queues = ["identity"],
            SchedulePollingInterval = TimeSpan.FromSeconds(1),
            StopTimeout = TimeSpan.FromSeconds(5),
            ShutdownTimeout = TimeSpan.FromSeconds(10),
            Activator = new AspNetCoreJobActivator(provider.GetRequiredService<IServiceScopeFactory>())
        }, storage);
        provider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<OutboxPump>("outbox-v1", job => job.RunAsync(CancellationToken.None), Cron.Minutely(), new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        provider.GetRequiredService<IBackgroundJobClient>().Enqueue<OutboxPump>(job => job.RunAsync(CancellationToken.None));
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) { server?.SendStop(); server?.Dispose(); server = null; return Task.CompletedTask; }
    public void Dispose() { server?.Dispose(); server = null; }
}
