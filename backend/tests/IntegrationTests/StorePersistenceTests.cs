using System.Data.Common;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Application.Shared.Storage;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HawalekNow.IntegrationTests;

public sealed class StorePersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    private readonly IFileStorage files = new LocalFileStorage(StoreTestData.Root(), TimeProvider.System);
    public Task InitializeAsync() { _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Concurrent_creation_and_actual_unique_constraint_allow_one_store_and_one_audit()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var activity = await StoreTestData.ActivityAsync(database);
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var results = await Task.WhenAll(StoreTestData.Service(database, first, files).CreateAsync(owner.Id, new(activity, StoreTestData.Details()), default),
            StoreTestData.Service(database, second, files).CreateAsync(owner.Id, new(activity, StoreTestData.Details()), default));
        var created = Assert.Single(results, value => value.IsSuccess).Value; Assert.Single(results, value => value.Error?.Kind == ErrorKind.Conflict);
        await using var read = database.CreateContext();
        Assert.Single(await read.Set<Store>().Where(value => value.MerchantAccountId == owner.ProfileId).ToArrayAsync());
        Assert.Single(await read.Set<AuditEntry>().Where(value => value.TargetId == created.Id && value.Operation == "Store.Create").ToArrayAsync());
        Assert.True(StoreDetails.TryCreate("Duplicate", "+201112345678", null, null, null, null, out var details));
        read.Add(Store.Create(owner.ProfileId, activity, details!));
        await Assert.ThrowsAsync<DbUpdateException>(() => read.SaveChangesAsync());
    }

    [Fact]
    public async Task Activity_contract_and_SQL_foreign_key_reject_unavailable_choices_and_reference_deletion()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var disabled = await StoreTestData.ActivityAsync(database, false);
        await using (var context = database.CreateContext())
        {
            var service = StoreTestData.Service(database, context, files);
            Assert.Equal(ErrorKind.Validation, (await service.CreateAsync(owner.Id, new(disabled, StoreTestData.Details()), default)).Error?.Kind);
            Assert.Equal(ErrorKind.Validation, (await service.CreateAsync(owner.Id, new(Guid.NewGuid(), StoreTestData.Details()), default)).Error?.Kind);
        }
        var store = await StoreTestData.CreateAsync(database, owner, files);
        await using var read = database.CreateContext();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => read.Set<Activity>().Where(value => value.Id == store.ActivityId).ExecuteDeleteAsync());
        Assert.True(await read.Set<Activity>().AnyAsync(value => value.Id == store.ActivityId)); Assert.False(read.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Ownership_live_identity_status_and_stale_versions_are_checked_at_the_service_boundary()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var other = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        var customer = await MerchantTestAccounts.CreateAsync(factory, AccountType.Customer); var store = await StoreTestData.CreateAsync(database, owner, files);
        await using var context = database.CreateContext(); var service = StoreTestData.Service(database, context, files);
        Assert.Equal(ErrorKind.NotFound, (await service.UpdateAsync(other.Id, store.Id, new(store.RowVersion, StoreTestData.Details("forged")), default)).Error?.Kind);
        Assert.Equal(ErrorKind.Forbidden, (await service.UpdateAsync(customer.Id, store.Id, new(store.RowVersion, StoreTestData.Details("forged")), default)).Error?.Kind);
        var changed = await service.UpdateAsync(owner.Id, store.Id, new(store.RowVersion, StoreTestData.Details("Updated")), default); Assert.True(changed.IsSuccess);
        Assert.Equal(store.ActivityId, changed.Value.ActivityId);
        Assert.Equal(ErrorKind.Conflict, (await service.UpdateAsync(owner.Id, store.Id, new(store.RowVersion, StoreTestData.Details("stale")), default)).Error?.Kind);
        await context.Set<ApplicationUser>().Where(value => value.Id == owner.Id).ExecuteUpdateAsync(set => set.SetProperty(value => value.EmailConfirmed, false));
        Assert.Equal(ErrorKind.Forbidden, (await service.OwnAsync(owner.Id, default)).Error?.Kind);
    }

    [Fact]
    public async Task Failure_after_SQL_save_rolls_back_reference_audit_and_outbox_and_compensates_only_the_new_file()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var store = await StoreTestData.CreateAsync(database, owner, files);
        await using (var initial = database.CreateContext()) store = (await StoreTestData.Service(database, initial, files).ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png()), default)).Value;
        string oldKey; await using (var read = database.CreateContext()) oldKey = (await read.Set<Store>().SingleAsync(value => value.Id == store.Id)).LogoKey!;
        var failure = new FailAfterLogoSaved();
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString).AddInterceptors(failure).Options;
        await using (var context = new HawalekDbContext(options))
        {
            var result = await StoreTestData.Service(database, context, files).ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png(2, 2)), default);
            Assert.Equal(ErrorKind.Unavailable, result.Error?.Kind);
        }
        Assert.True(failure.SawReferenceAndAuditAndIntent); Assert.NotNull(failure.NewKey);
        await using var verify = database.CreateContext(); var after = await verify.Set<Store>().SingleAsync(value => value.Id == store.Id);
        Assert.Equal(oldKey, after.LogoKey); Assert.Equal(store.RowVersion, Convert.ToBase64String(after.RowVersion));
        Assert.Single(await verify.Set<AuditEntry>().Where(value => value.TargetId == store.Id && value.Operation == "Store.ChangeLogo").ToArrayAsync());
        Assert.False(await verify.Set<OutboxMessage>().AnyAsync(value => value.Kind == LogoDeleteHandler.KindName && value.SubjectId == owner.Id));
        Assert.NotNull(await files.ReadImageAsync(oldKey, default)); Assert.Null(await files.ReadImageAsync(failure.NewKey!, default));
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_never_deletes_a_file_that_SQL_already_references()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var store = await StoreTestData.CreateAsync(database, owner, files);
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString).AddInterceptors(new FailAfterCommit()).Options;
        await using (var context = new HawalekDbContext(options))
        {
            var result = await StoreTestData.Service(database, context, files).ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png()), default);
            Assert.Equal(ErrorKind.Unavailable, result.Error?.Kind);
        }
        await using var read = database.CreateContext(); var after = await read.Set<Store>().SingleAsync(value => value.Id == store.Id);
        Assert.NotNull(after.LogoKey); Assert.NotNull(await files.ReadImageAsync(after.LogoKey, default));
        Assert.Single(await read.Set<AuditEntry>().Where(value => value.TargetId == store.Id && value.Operation == "Store.ChangeLogo").ToArrayAsync());
    }

    [Fact]
    public async Task Replacement_and_removal_defer_owned_file_deletion_until_the_committed_outbox_is_handled()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var store = await StoreTestData.CreateAsync(database, owner, files);
        string oldKey; string newKey;
        await using (var context = database.CreateContext())
        {
            var service = StoreTestData.Service(database, context, files);
            store = (await service.ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png()), default)).Value;
            oldKey = (await context.Set<Store>().SingleAsync(value => value.Id == store.Id)).LogoKey!;
            store = (await service.ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, new MemoryStream(StorageTestImages.Png(2, 1)), default)).Value;
            newKey = (await context.Set<Store>().SingleAsync(value => value.Id == store.Id)).LogoKey!;
        }
        Assert.NotEqual(oldKey, newKey); Assert.NotNull(await files.ReadImageAsync(oldKey, default)); Assert.NotNull(await files.ReadImageAsync(newKey, default));
        await using (var read = database.CreateContext())
        {
            var intent = Assert.Single(await read.Set<OutboxMessage>().Where(value => value.Kind == LogoDeleteHandler.KindName && value.SubjectId == owner.Id).ToArrayAsync());
            Assert.Equal(OutboxState.Pending, intent.State); Assert.DoesNotContain(oldKey, intent.ProtectedPayload);
            var payload = new PayloadProtection(TestIdentityConfiguration.Create(database.ConnectionString, database.IdentifierKey)).Unprotect<LogoDeletePayload>(intent.Kind, intent.ProtectedPayload);
            var cleanup = new LogoCleanup(read, files, new StoredFileLock(read)); Assert.True(await cleanup.DeleteUnreferencedAsync(payload.Key, default));
            Assert.False(await cleanup.DeleteUnreferencedAsync(newKey, default));
        }
        Assert.Null(await files.ReadImageAsync(oldKey, default)); Assert.NotNull(await files.ReadImageAsync(newKey, default));
        await using (var context = database.CreateContext()) Assert.False((await StoreTestData.Service(database, context, files).ChangeLogoAsync(owner.Id, store.Id, store.RowVersion, null, default)).Value.HasLogo);
        await using (var context = database.CreateContext()) Assert.True(await new LogoCleanup(context, files, new StoredFileLock(context)).DeleteUnreferencedAsync(newKey, default));
        Assert.Null(await files.ReadImageAsync(newKey, default));
    }

    private sealed class FailAfterLogoSaved : SaveChangesInterceptor
    {
        private bool armed = true;
        public string? NewKey { get; private set; }
        public bool SawReferenceAndAuditAndIntent { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (armed && eventData.Context is HawalekDbContext context && context.ChangeTracker.Entries<Store>().SingleOrDefault()?.Entity is { LogoKey: not null } store)
            {
                armed = false; NewKey = store.LogoKey;
                SawReferenceAndAuditAndIntent = context.Database.CurrentTransaction is not null && await context.Set<Store>().AsNoTracking().AnyAsync(value => value.Id == store.Id && value.LogoKey == NewKey, cancellationToken)
                    && await context.Set<AuditEntry>().AnyAsync(value => value.TargetId == store.Id && value.Operation == "Store.ChangeLogo", cancellationToken)
                    && await context.Set<OutboxMessage>().AnyAsync(value => value.Kind == LogoDeleteHandler.KindName, cancellationToken);
                throw new IOException("test-only SQL save failure");
            }
            return result;
        }
    }
    private sealed class FailAfterCommit : DbTransactionInterceptor
    {
        private bool armed = true;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (armed) { armed = false; throw new IOException("test-only commit acknowledgement failure"); }
            return Task.CompletedTask;
        }
    }
}
