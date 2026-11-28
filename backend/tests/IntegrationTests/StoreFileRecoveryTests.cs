using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class StoreFileRecoveryTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    [Fact]
    public async Task Reconciliation_uses_real_SQL_references_and_retains_modified_files_after_restart()
    {
        var root = StoreTestData.Root(); var clock = new ManualTimeProvider(TimeProvider.System.GetUtcNow()); var files = new LocalFileStorage(root, clock);
        await using var factory = new IdentityApiFactory(database, clock, settings: new Dictionary<string, string?> { ["FileStorage:Root"] = root });
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var store = await StoreTestData.CreateAsync(database, owner, files);
        string linked;
        await using (var context = database.CreateContext())
        {
            Assert.True((await StoreTestData.Service(database, context, files, clock).ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png()), default)).IsSuccess);
            linked = (await context.Set<Store>().SingleAsync(value => value.Id == store.Id)).LogoKey!;
        }
        var orphan = Guid.NewGuid().ToString("N"); var changed = Guid.NewGuid().ToString("N");
        await files.WriteImageAsync(orphan, new MemoryStream(StorageTestImages.Png()), default); await files.WriteImageAsync(changed, new MemoryStream(StorageTestImages.Png()), default);
        var changedPath = Path.Combine(root, changed + ".png"); await File.WriteAllTextAsync(changedPath, "foreign-replacement-retain");
        clock.Advance(TimeSpan.FromHours(25));
        await using (var context = database.CreateContext())
        {
            var restarted = new LocalFileStorage(root, clock);
            var job = new LogoReconciliationJob(restarted, new LogoCleanup(context, restarted, new StoredFileLock(context)), clock);
            Assert.Equal(1, await job.RunAsync(default)); Assert.Equal(0, await job.RunAsync(default));
        }
        var after = new LocalFileStorage(root, clock); Assert.NotNull(await after.ReadImageAsync(linked, default)); Assert.Null(await after.ReadImageAsync(orphan, default));
        Assert.Equal("foreign-replacement-retain", await File.ReadAllTextAsync(changedPath));
    }

    [Fact]
    public async Task Committed_deletion_outbox_retries_a_real_integrity_failure_then_completes_idempotently()
    {
        // This pump fixture owns a separate database so it cannot consume another fixture's intents or file root.
        await using var isolated = new SqlDatabase(); await isolated.InitializeAsync();
        var root = StoreTestData.Root(); var clock = new ManualTimeProvider(TimeProvider.System.GetUtcNow()); var files = new LocalFileStorage(root, clock);
        await using var factory = new IdentityApiFactory(isolated, clock, settings: new Dictionary<string, string?> { ["FileStorage:Root"] = root });
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var store = await StoreTestData.CreateAsync(isolated, owner, files);
        string oldKey; string newKey;
        await using (var context = isolated.CreateContext())
        {
            var service = StoreTestData.Service(isolated, context, files, clock);
            store = (await service.ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png()), default)).Value;
            oldKey = (await context.Set<Store>().SingleAsync(value => value.Id == store.Id)).LogoKey!;
            store = (await service.ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png(2, 2)), default)).Value;
            newKey = (await context.Set<Store>().SingleAsync(value => value.Id == store.Id)).LogoKey!;
        }
        var path = Path.Combine(root, oldKey + ".png"); var original = await File.ReadAllBytesAsync(path); await File.WriteAllTextAsync(path, "foreign-content-must-not-delete");
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(default);
        Assert.Equal("foreign-content-must-not-delete", await File.ReadAllTextAsync(path));
        await using (var read = isolated.CreateContext())
        {
            var intent = await read.Set<OutboxMessage>().SingleAsync(value => value.Kind == LogoDeleteHandler.KindName);
            Assert.Equal(OutboxState.Pending, intent.State); Assert.Equal(1, intent.Attempts); Assert.Equal("delivery.failed", intent.LastFailureCode); Assert.NotEmpty(intent.ProtectedPayload);
        }
        await File.WriteAllBytesAsync(path, original); clock.Advance(TimeSpan.FromSeconds(11));
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(default);
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(default);
        Assert.Null(await files.ReadImageAsync(oldKey, default)); Assert.NotNull(await files.ReadImageAsync(newKey, default));
        await using var verify = isolated.CreateContext(); var sent = await verify.Set<OutboxMessage>().SingleAsync(value => value.Kind == LogoDeleteHandler.KindName);
        Assert.Equal(OutboxState.Sent, sent.State); Assert.Equal(2, sent.Attempts); Assert.Equal("", sent.ProtectedPayload);
    }
}
