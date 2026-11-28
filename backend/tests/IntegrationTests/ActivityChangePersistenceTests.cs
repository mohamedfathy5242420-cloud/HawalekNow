using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HawalekNow.IntegrationTests;

public sealed class ActivityChangePersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    public Task InitializeAsync() { _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private async Task<(MerchantTestAccount Owner, MerchantTestAccount Admin, StoreDto Store, Guid Target)> SetupAsync()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var store = await StoreTestData.CreateAsync(database, owner, new LocalFileStorage(StoreTestData.Root(), TimeProvider.System));
        return (owner, admin, store, await StoreTestData.ActivityAsync(database));
    }
    [Fact]
    public async Task Pending_request_does_not_change_store_and_approval_commits_activity_decision_and_audit_together()
    {
        var setup = await SetupAsync(); await using var context = database.CreateContext(); var service = ActivityChangeTestData.Service(context);
        var request = (await service.RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Target, setup.Store.RowVersion, "new business direction"), default)).Value;
        Assert.Equal(setup.Store.ActivityId, request.CurrentActivityId); Assert.Equal(setup.Store.RowVersion, request.StoreRowVersion); Assert.Equal("Pending", request.Status);
        context.ChangeTracker.Clear(); Assert.Equal(setup.Store.ActivityId, (await context.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id)).ActivityId);
        var approved = await service.ReviewAsync(setup.Admin.Id, request.Id, true, new(request.RowVersion, request.StoreRowVersion, "approved scope"), default);
        Assert.True(approved.IsSuccess); Assert.Equal(setup.Target, approved.Value.CurrentActivityId); Assert.Equal("Approved", approved.Value.Status); Assert.NotEqual(request.StoreRowVersion, approved.Value.StoreRowVersion);
        context.ChangeTracker.Clear(); Assert.Equal(setup.Target, (await context.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id)).ActivityId);
        Assert.Equal(ActivityChangeStatus.Approved, (await context.Set<StoreActivityChange>().SingleAsync(value => value.Id == request.Id)).Status);
        var audit = await context.Set<AuditEntry>().SingleAsync(value => value.TargetId == setup.Store.Id && value.Operation == "Store.ActivityChange.Approve");
        Assert.Equal(setup.Admin.Id, audit.ActorId); Assert.Equal("approved scope", audit.Reason); Assert.Equal(setup.Store.ActivityId.ToString("N"), audit.PreviousState); Assert.Equal(setup.Target.ToString("N"), audit.CurrentState);
        Assert.False(context.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task Rejection_preserves_activity_and_store_version_and_allows_a_new_request()
    {
        var setup = await SetupAsync(); await using var context = database.CreateContext(); var service = ActivityChangeTestData.Service(context);
        var request = (await service.RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Target, setup.Store.RowVersion), default)).Value;
        var rejected = await service.ReviewAsync(setup.Admin.Id, request.Id, false, new(request.RowVersion, request.StoreRowVersion, "outside approved scope"), default);
        Assert.True(rejected.IsSuccess); Assert.Equal("Rejected", rejected.Value.Status); Assert.Equal(setup.Store.ActivityId, rejected.Value.CurrentActivityId); Assert.Equal(setup.Store.RowVersion, rejected.Value.StoreRowVersion);
        Assert.True((await service.RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Target, setup.Store.RowVersion), default)).IsSuccess);
        Assert.Equal(2, await context.Set<StoreActivityChange>().CountAsync(value => value.StoreId == setup.Store.Id));
        Assert.Single(await context.Set<StoreActivityChange>().Where(value => value.StoreId == setup.Store.Id && value.Status == ActivityChangeStatus.Pending).ToArrayAsync());
    }
    [Fact]
    public async Task Competing_requests_and_opposing_admin_decisions_each_commit_one_winner()
    {
        var setup = await SetupAsync(); var anotherTarget = await StoreTestData.ActivityAsync(database); var otherAdmin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var requests = await Task.WhenAll(ActivityChangeTestData.Service(first).RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Target, setup.Store.RowVersion), default),
            ActivityChangeTestData.Service(second).RequestAsync(setup.Owner.Id, setup.Store.Id, new(anotherTarget, setup.Store.RowVersion), default));
        Assert.Single(requests, value => value.IsSuccess); Assert.Single(requests, value => value.Error?.Kind == ErrorKind.Conflict); var winner = requests.Single(value => value.IsSuccess).Value;
        first.ChangeTracker.Clear(); second.ChangeTracker.Clear();
        var reviews = await Task.WhenAll(ActivityChangeTestData.Service(first).ReviewAsync(setup.Admin.Id, winner.Id, true, new(winner.RowVersion, winner.StoreRowVersion), default),
            ActivityChangeTestData.Service(second).ReviewAsync(otherAdmin.Id, winner.Id, false, new(winner.RowVersion, winner.StoreRowVersion, "reject proposal"), default));
        Assert.Single(reviews, value => value.IsSuccess); Assert.Single(reviews, value => value.Error?.Kind == ErrorKind.Conflict);
        await using var read = database.CreateContext(); var request = await read.Set<StoreActivityChange>().SingleAsync(value => value.StoreId == setup.Store.Id);
        Assert.Equal(request.Status == ActivityChangeStatus.Approved ? request.RequestedActivityId : request.PreviousActivityId, (await read.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id)).ActivityId);
        Assert.Single(await read.Set<AuditEntry>().Where(value => value.TargetId == setup.Store.Id && (value.Operation == "Store.ActivityChange.Approve" || value.Operation == "Store.ActivityChange.Reject")).ToArrayAsync());
    }
    [Fact]
    public async Task Physical_pending_unique_index_and_activity_FKs_protect_against_direct_SQL_writers()
    {
        var setup = await SetupAsync(); await using var context = database.CreateContext();
        Assert.True((await ActivityChangeTestData.Service(context).RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Target, setup.Store.RowVersion), default)).IsSuccess);
        context.Add(StoreActivityChange.Create(setup.Store.Id, setup.Store.ActivityId, setup.Target, setup.Owner.Id, null, DateTimeOffset.UtcNow));
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); Assert.Contains(Assert.IsType<SqlException>(duplicate.InnerException).Number, new[] { 2601, 2627 });
        context.ChangeTracker.Clear(); var deletion = await Assert.ThrowsAsync<SqlException>(() => context.Set<Activity>().Where(value => value.Id == setup.Target).ExecuteDeleteAsync()); Assert.Equal(547, deletion.Number);
        var wrongTarget = Guid.NewGuid(); var invalid = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync($"UPDATE [merchants].[ActivityChanges] SET [RequestedActivityId]={wrongTarget} WHERE [StoreId]={setup.Store.Id}")); Assert.Equal(547, invalid.Number);
    }
    [Fact]
    public async Task Approval_rechecks_current_activity_availability_and_stale_store_versions_are_conflicts()
    {
        var setup = await SetupAsync(); await using var context = database.CreateContext(); var service = ActivityChangeTestData.Service(context);
        var inactive = await StoreTestData.ActivityAsync(database, false);
        Assert.Equal(ErrorKind.Validation, (await service.RequestAsync(setup.Owner.Id, setup.Store.Id, new(inactive, setup.Store.RowVersion), default)).Error?.Kind);
        Assert.Equal(ErrorKind.Validation, (await service.RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Store.ActivityId, setup.Store.RowVersion), default)).Error?.Kind);
        var request = (await service.RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Target, setup.Store.RowVersion), default)).Value;
        await context.Set<Activity>().Where(value => value.Id == setup.Target).ExecuteUpdateAsync(set => set.SetProperty(value => value.IsActive, false));
        Assert.Equal(ErrorKind.Conflict, (await service.ReviewAsync(setup.Admin.Id, request.Id, true, new(request.RowVersion, request.StoreRowVersion), default)).Error?.Kind);
        await context.Set<Store>().Where(value => value.Id == setup.Store.Id).ExecuteUpdateAsync(set => set.SetProperty(value => value.Name, "New name"));
        Assert.Equal(ErrorKind.Conflict, (await service.ReviewAsync(setup.Admin.Id, request.Id, false, new(request.RowVersion, request.StoreRowVersion, "reject"), default)).Error?.Kind);
        context.ChangeTracker.Clear(); var stored = await context.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id);
        Assert.True((await service.ReviewAsync(setup.Admin.Id, request.Id, false, new(request.RowVersion, Convert.ToBase64String(stored.RowVersion), "unavailable target"), default)).IsSuccess);
        Assert.Equal(setup.Store.ActivityId, stored.ActivityId);
    }
    [Fact]
    public async Task Failure_after_SQL_save_rolls_back_activity_request_decision_and_audit()
    {
        var setup = await SetupAsync(); ActivityChangeDto request;
        await using (var context = database.CreateContext()) request = (await ActivityChangeTestData.Service(context).RequestAsync(setup.Owner.Id, setup.Store.Id, new(setup.Target, setup.Store.RowVersion), default)).Value;
        var failure = new FailAfterApprovalSaved(request.Id, setup.Store.Id, setup.Target);
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).AddInterceptors(failure).Options;
        await using (var writer = new HawalekDbContext(options)) await Assert.ThrowsAsync<IOException>(() => ActivityChangeTestData.Service(writer).ReviewAsync(setup.Admin.Id, request.Id, true, new(request.RowVersion, request.StoreRowVersion), default));
        Assert.True(failure.SawAllInsideTransaction); await using var read = database.CreateContext();
        Assert.Equal(setup.Store.ActivityId, (await read.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id)).ActivityId);
        Assert.Equal(ActivityChangeStatus.Pending, (await read.Set<StoreActivityChange>().SingleAsync(value => value.Id == request.Id)).Status);
        Assert.False(await read.Set<AuditEntry>().AnyAsync(value => value.TargetId == setup.Store.Id && value.Operation == "Store.ActivityChange.Approve"));
    }
    private sealed class FailAfterApprovalSaved(Guid requestId, Guid storeId, Guid activityId) : SaveChangesInterceptor
    {
        public bool SawAllInsideTransaction { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is HawalekDbContext context)
            {
                SawAllInsideTransaction = context.Database.CurrentTransaction is not null && await context.Set<Store>().AsNoTracking().AnyAsync(value => value.Id == storeId && value.ActivityId == activityId, cancellationToken)
                    && await context.Set<StoreActivityChange>().AsNoTracking().AnyAsync(value => value.Id == requestId && value.Status == ActivityChangeStatus.Approved, cancellationToken)
                    && await context.Set<AuditEntry>().AnyAsync(value => value.TargetId == storeId && value.Operation == "Store.ActivityChange.Approve", cancellationToken);
                throw new IOException("test-only failure after real activity approval Save");
            }
            return result;
        }
    }
}
