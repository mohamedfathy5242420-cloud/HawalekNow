using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HawalekNow.IntegrationTests;

public sealed class StoreAdministrationPersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    public Task InitializeAsync() { _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private async Task<(MerchantTestAccount Owner, MerchantTestAccount Admin, StoreDto Store)> SetupAsync()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        return (owner, admin, await StoreTestData.CreateAsync(database, owner, new LocalFileStorage(StoreTestData.Root(), TimeProvider.System)));
    }
    [Fact]
    public async Task Moderation_and_audit_commit_with_reason_actor_time_and_both_state_flags()
    {
        var setup = await SetupAsync(); await using var context = database.CreateContext();
        var service = StoreAdministrationTestData.Service(database, context, new LocalFileStorage(StoreTestData.Root(), TimeProvider.System));
        var hidden = await service.ModerateAsync(setup.Admin.Id, setup.Store.Id, StoreAdministrationAction.Hide, new(setup.Store.RowVersion, "visibility review"), default);
        Assert.True(hidden.IsSuccess); Assert.True(hidden.Value.IsHidden); Assert.True(hidden.Value.CanOwnerEdit); Assert.False(hidden.Value.IsSuspended);
        var audit = await context.Set<AuditEntry>().SingleAsync(value => value.TargetId == setup.Store.Id && value.Operation == "Store.Hide");
        Assert.Equal(setup.Admin.Id, audit.ActorId); Assert.Equal("visibility review", audit.Reason); Assert.Equal("Hidden=False;Suspended=False", audit.PreviousState); Assert.Equal("Hidden=True;Suspended=False", audit.CurrentState);
        var suspended = await service.ModerateAsync(setup.Admin.Id, setup.Store.Id, StoreAdministrationAction.Suspend, new(hidden.Value.RowVersion, "commercial review"), default);
        Assert.True(suspended.IsSuccess); Assert.True(suspended.Value.IsHidden); Assert.True(suspended.Value.IsSuspended); Assert.False(suspended.Value.CanOwnerEdit);
        var resumed = await service.ModerateAsync(setup.Admin.Id, setup.Store.Id, StoreAdministrationAction.Resume, new(suspended.Value.RowVersion, "resolved"), default);
        Assert.True(resumed.IsSuccess); Assert.True(resumed.Value.IsHidden); Assert.False(resumed.Value.IsSuspended);
        context.ChangeTracker.Clear(); var stored = await context.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id);
        Assert.Equal("resolved", stored.SuspensionReason); Assert.Equal("visibility review", stored.VisibilityReason); Assert.False(context.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task Competing_admin_state_changes_commit_only_one_version_and_audit()
    {
        var setup = await SetupAsync(); var secondAdmin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        await using var first = database.CreateContext(); await using var second = database.CreateContext(); var files = new LocalFileStorage(StoreTestData.Root(), TimeProvider.System);
        var results = await Task.WhenAll(StoreAdministrationTestData.Service(database, first, files).ModerateAsync(setup.Admin.Id, setup.Store.Id, StoreAdministrationAction.Hide, new(setup.Store.RowVersion, "hide"), default),
            StoreAdministrationTestData.Service(database, second, files).ModerateAsync(secondAdmin.Id, setup.Store.Id, StoreAdministrationAction.Suspend, new(setup.Store.RowVersion, "suspend"), default));
        Assert.Single(results, value => value.IsSuccess); Assert.Single(results, value => value.Error?.Kind == ErrorKind.Conflict);
        await using var read = database.CreateContext(); var store = await read.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id); Assert.NotEqual(store.IsHidden, store.IsSuspended);
        Assert.Single(await read.Set<AuditEntry>().Where(value => value.TargetId == setup.Store.Id && (value.Operation == "Store.Hide" || value.Operation == "Store.Suspend")).ToArrayAsync());
    }
    [Fact]
    public async Task A_race_between_owner_edit_and_admin_suspension_cannot_overwrite_the_other_version()
    {
        var setup = await SetupAsync(); await using var owner = database.CreateContext(); await using var admin = database.CreateContext(); var files = new LocalFileStorage(StoreTestData.Root(), TimeProvider.System);
        var edit = StoreTestData.Service(database, owner, files).UpdateAsync(setup.Owner.Id, setup.Store.Id, new(setup.Store.RowVersion, StoreTestData.Details("Owner edit")), default);
        var suspend = StoreAdministrationTestData.Service(database, admin, files).ModerateAsync(setup.Admin.Id, setup.Store.Id, StoreAdministrationAction.Suspend, new(setup.Store.RowVersion, "suspend"), default);
        var results = await Task.WhenAll(edit, suspend); Assert.Single(results, value => value.IsSuccess);
        Assert.Single(results, value => value.Error?.Kind is ErrorKind.Conflict or ErrorKind.Forbidden);
        await using var read = database.CreateContext(); var store = await read.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id);
        if (store.IsSuspended) Assert.Equal(setup.Store.Name, store.Name); else Assert.Equal("Owner edit", store.Name);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_real_save_failure_rolls_back_admin_state_or_details_and_audit(bool editDetails)
    {
        var setup = await SetupAsync(); var operation = editDetails ? "Store.AdminEdit" : "Store.Hide"; var failure = new FailAfterAdminSaved(setup.Store.Id, operation);
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).AddInterceptors(failure).Options;
        await using (var writer = new HawalekDbContext(options))
        {
            var service = StoreAdministrationTestData.Service(database, writer, new LocalFileStorage(StoreTestData.Root(), TimeProvider.System));
            if (editDetails) await Assert.ThrowsAsync<IOException>(() => service.UpdateAsync(setup.Admin.Id, setup.Store.Id, new(setup.Store.RowVersion, StoreTestData.Details("Admin edit"), "correct details"), default));
            else await Assert.ThrowsAsync<IOException>(() => service.ModerateAsync(setup.Admin.Id, setup.Store.Id, StoreAdministrationAction.Hide, new(setup.Store.RowVersion, "hide reason"), default));
        }
        Assert.True(failure.SawBothInsideTransaction); await using var read = database.CreateContext(); var stored = await read.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id);
        Assert.False(stored.IsHidden); Assert.Equal(setup.Store.Name, stored.Name); Assert.False(await read.Set<AuditEntry>().AnyAsync(value => value.TargetId == setup.Store.Id && value.Operation == operation));
    }
    [Fact]
    public async Task Admin_branch_edit_uses_the_same_atomic_validation_and_keeps_store_ownership_immutable()
    {
        var setup = await SetupAsync(); var path = await BranchTestData.PathAsync(database); BranchDto branch;
        await using (var context = database.CreateContext()) branch = (await BranchTestData.Service(context).CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(path), default)).Value;
        var failure = new FailAfterAdminSaved(branch.Id, "Branch.AdminEdit");
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).AddInterceptors(failure).Options;
        await using (var writer = new HawalekDbContext(options))
            await Assert.ThrowsAsync<IOException>(() => StoreAdministrationTestData.Service(database, writer, new LocalFileStorage(StoreTestData.Root(), TimeProvider.System)).UpdateBranchAsync(setup.Admin.Id, setup.Store.Id, branch.Id, new(branch.RowVersion, BranchTestData.Write(path) with { Name = "Admin corrected" }, "correct address"), default));
        Assert.True(failure.SawBothInsideTransaction); await using var read = database.CreateContext(); var stored = await read.Set<MerchantBranch>().SingleAsync(value => value.Id == branch.Id);
        Assert.Equal(branch.Name, stored.Name); Assert.Equal(setup.Store.Id, stored.StoreId); Assert.False(await read.Set<AuditEntry>().AnyAsync(value => value.TargetId == branch.Id && value.Operation == "Branch.AdminEdit"));
    }
    [Fact]
    public async Task SQL_requires_moderation_reasons_and_service_checks_current_admin_role()
    {
        var setup = await SetupAsync(); await using var context = database.CreateContext();
        var invalid = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync($"UPDATE [merchants].[Stores] SET [IsHidden]=1,[VisibilityReason]=NULL WHERE [Id]={setup.Store.Id}")); Assert.Equal(547, invalid.Number);
        await context.Set<ApplicationUser>().Where(value => value.Id == setup.Admin.Id).ExecuteUpdateAsync(set => set.SetProperty(value => value.AccountType, AccountType.Customer));
        var result = await StoreAdministrationTestData.Service(database, context, new LocalFileStorage(StoreTestData.Root(), TimeProvider.System)).ModerateAsync(setup.Admin.Id, setup.Store.Id, StoreAdministrationAction.Hide, new(setup.Store.RowVersion, "reason"), default);
        Assert.Equal(ErrorKind.Forbidden, result.Error?.Kind); Assert.False(await context.Set<AuditEntry>().AnyAsync(value => value.TargetId == setup.Store.Id && value.Operation == "Store.Hide"));
    }
    private sealed class FailAfterAdminSaved(Guid id, string operation) : SaveChangesInterceptor
    {
        public bool SawBothInsideTransaction { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is HawalekDbContext context)
            {
                var changed = operation == "Branch.AdminEdit" ? await context.Set<MerchantBranch>().AsNoTracking().AnyAsync(value => value.Id == id && value.Name == "Admin corrected", cancellationToken)
                    : await context.Set<Store>().AsNoTracking().AnyAsync(value => value.Id == id && (operation == "Store.Hide" ? value.IsHidden : value.Name == "Admin edit"), cancellationToken);
                SawBothInsideTransaction = context.Database.CurrentTransaction is not null && changed && await context.Set<AuditEntry>().AnyAsync(value => value.TargetId == id && value.Operation == operation, cancellationToken);
                throw new IOException("test-only failure after actual administrative Save");
            }
            return result;
        }
    }
}
