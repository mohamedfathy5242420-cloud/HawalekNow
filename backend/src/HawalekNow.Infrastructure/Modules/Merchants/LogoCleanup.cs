using Hangfire;
using HawalekNow.Application.Shared.Messaging;
using HawalekNow.Application.Shared.Storage;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HawalekNow.Infrastructure.Modules.Merchants;

public sealed record LogoDeletePayload(string Key);
public sealed class LogoDeletionIntents(OutboxWriter writer, PayloadProtection protection)
{
    public void Add(string key, Guid ownerId)
    {
        if (!ImageStorageLimits.ValidKey(key)) throw new InvalidOperationException("A generated image key is required.");
        writer.Add(LogoDeleteHandler.KindName, protection.Protect(LogoDeleteHandler.KindName, new LogoDeletePayload(key)), ownerId);
    }
}

public sealed class LogoCleanup(HawalekDbContext context, IFileStorage files, StoredFileLock fileLock)
{
    // Called only outside the store mutation transaction. Each deletion checks committed references anew.
    public async Task<bool> DeleteUnreferencedAsync(string key, CancellationToken cancellationToken)
    {
        await using var transaction = await context.BeginAsync(cancellationToken);
        await fileLock.AcquireAsync(key, cancellationToken);
        if (await context.Set<Store>().AsNoTracking().AnyAsync(value => value.LogoKey == key, cancellationToken)) return false;
        await files.DeleteOwnedAsync(key, cancellationToken);
        await transaction.CommitAsync(cancellationToken); return true;
    }
    public async Task TryCompensateAsync(string? key)
    {
        if (key is null) return;
        try { context.ChangeTracker.Clear(); await DeleteUnreferencedAsync(key, CancellationToken.None); }
        catch (Exception) { /* Keep the private owned object when SQL/storage is unavailable; the reconciliation job retries later. */ }
    }
}

public sealed class LogoDeleteHandler(IServiceProvider services, PayloadProtection protection) : IOutboxHandler
{
    public const string KindName = "Merchants.DeleteLogo.v1";
    public string Kind => KindName;
    public async Task<OutboxHandlingResult> HandleAsync(Guid messageId, string protectedPayload, CancellationToken cancellationToken)
    {
        var payload = protection.Unprotect<LogoDeletePayload>(Kind, protectedPayload);
        // Resolve storage only for this message kind; a storage failure must not prevent OTP dispatch.
        var deleted = await services.GetRequiredService<LogoCleanup>().DeleteUnreferencedAsync(payload.Key, cancellationToken);
        return deleted ? OutboxHandlingResult.Completed : OutboxHandlingResult.Suppressed;
    }
}

public sealed class LogoReconciliationJob(IFileStorage files, LogoCleanup cleanup, TimeProvider clock)
{
    [Queue("storage"), AutomaticRetry(Attempts = 3)]
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var deleted = 0;
        await foreach (var image in files.OldImagesAsync(clock.GetUtcNow().AddHours(-24), cancellationToken))
        {
            try { if (await cleanup.DeleteUnreferencedAsync(image.Key, cancellationToken)) deleted++; }
            catch (IOException) { /* Retain changed/unavailable files. Ownership must never be guessed. */ }
            catch (UnauthorizedAccessException) { }
        }
        return deleted;
    }
}

public sealed class MerchantMaintenanceScheduler(IServiceProvider services, IConfiguration configuration) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (configuration.GetValue("Background:Enabled", true))
            services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<LogoReconciliationJob>("merchant-logo-reconciliation-v1",
                job => job.RunAsync(CancellationToken.None), Cron.Hourly(), new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
