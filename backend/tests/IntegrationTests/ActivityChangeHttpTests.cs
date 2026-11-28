using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class ActivityChangeHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly ManualTimeProvider clock = new(TimeProvider.System.GetUtcNow());
    private IdentityApiFactory factory = null!;
    public Task InitializeAsync() { factory = new(database, clock); _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private async Task<(MerchantTestAccount Owner, MerchantTestAccount Admin, StoreDto Store, Guid Target)> SetupAsync()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        return (owner, admin, await StoreTestData.CreateAsync(database, owner, new LocalFileStorage(StoreTestData.Root(), clock)), await StoreTestData.ActivityAsync(database));
    }
    private static string OwnerRoute(Guid id) => $"/api/v1/merchants/stores/{id}/activity-change-requests";
    private static string ReviewRoute(Guid id, bool approve) => $"/api/v1/admin/store-activity-changes/{id}/" + (approve ? "approve" : "reject");
    private static async Task<ActivityChangeDto> RequestAsync(HttpClient client, StoreDto store, Guid target)
    {
        using var response = await client.PostBrowserJsonAsync(OwnerRoute(store.Id), new ActivityChangeWrite(target, store.RowVersion, "business change"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ActivityChangeDto>())!;
    }
    [Fact]
    public async Task Published_store_and_branches_keep_the_old_activity_through_pending_and_rejection_then_change_only_on_approval()
    {
        var setup = await SetupAsync(); using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        using (var approved = await admin.PostBrowserJsonAsync($"/api/v1/admin/merchants/{setup.Owner.ProfileId}/approve", new { rowVersion = await MerchantTestAccounts.VersionAsync(database, setup.Owner.ProfileId) })) Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        BranchDto branch;
        await using (var context = database.CreateContext()) branch = (await BranchTestData.Service(context).CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(await BranchTestData.PathAsync(database)), default)).Value;
        var request = await RequestAsync(owner, setup.Store, setup.Target);
        using (var current = await guest.GetAsync($"/api/v1/stores/{setup.Store.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, current.StatusCode); var json = await current.Content.ReadAsStringAsync();
            using var parsed = JsonDocument.Parse(json); Assert.Equal(setup.Store.ActivityId, parsed.RootElement.GetProperty("activityId").GetGuid()); Assert.DoesNotContain("requestedActivityId", json); Assert.DoesNotContain("rowVersion", json);
        }
        using (var existing = await guest.GetAsync($"/api/v1/branches/{branch.Id}")) Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        using (var rejected = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, false), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion, "revise scope"))) Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        using (var current = await guest.GetAsync($"/api/v1/stores/{setup.Store.Id}")) Assert.Equal(setup.Store.ActivityId, (await current.Content.ReadFromJsonAsync<PublicStoreDto>())!.ActivityId);
        request = await RequestAsync(owner, setup.Store, setup.Target);
        using (var approved = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, true), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion)))
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode); var result = (await approved.Content.ReadFromJsonAsync<ActivityChangeDto>())!; Assert.Equal("Approved", result.Status); Assert.Equal(setup.Target, result.CurrentActivityId);
        }
        using (var current = await guest.GetAsync($"/api/v1/stores/{setup.Store.Id}")) Assert.Equal(setup.Target, (await current.Content.ReadFromJsonAsync<PublicStoreDto>())!.ActivityId);
        using (var existing = await guest.GetAsync($"/api/v1/branches/{branch.Id}")) Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        using (var history = await owner.GetAsync(OwnerRoute(setup.Store.Id))) Assert.Equal(2, (await history.Content.ReadFromJsonAsync<MerchantPage<ActivityChangeDto>>())!.Items.Length);
    }
    [Fact]
    public async Task Ownership_live_admin_authorization_CSRF_versions_and_decision_reasons_are_enforced()
    {
        var setup = await SetupAsync(); var other = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var customer = await MerchantTestAccounts.CreateAsync(factory, AccountType.Customer);
        using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); using var stranger = factory.CreateHttpsClient(); using var consumer = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin); await MerchantTestAccounts.LoginAsync(stranger, other); await MerchantTestAccounts.LoginAsync(consumer, customer);
        using (var stolen = await stranger.PostBrowserJsonAsync(OwnerRoute(setup.Store.Id), new { activityId = setup.Target, storeRowVersion = setup.Store.RowVersion, actorId = setup.Owner.Id })) Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        using (var history = await stranger.GetAsync(OwnerRoute(setup.Store.Id))) Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        using (var forbidden = await consumer.PostBrowserJsonAsync(OwnerRoute(setup.Store.Id), new ActivityChangeWrite(setup.Target, setup.Store.RowVersion))) Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var missingCsrf = await owner.PostAsJsonAsync(OwnerRoute(setup.Store.Id), new ActivityChangeWrite(setup.Target, setup.Store.RowVersion))) Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        using (var badVersion = await owner.PostBrowserJsonAsync(OwnerRoute(setup.Store.Id), new ActivityChangeWrite(setup.Target, "invalid"))) Assert.Equal(HttpStatusCode.BadRequest, badVersion.StatusCode);
        var request = await RequestAsync(owner, setup.Store, setup.Target);
        using (var duplicate = await owner.PostBrowserJsonAsync(OwnerRoute(setup.Store.Id), new ActivityChangeWrite(setup.Target, setup.Store.RowVersion))) Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using (var denied = await stranger.PostBrowserJsonAsync(ReviewRoute(request.Id, true), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion))) Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var missingReason = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, false), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion))) Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        using (var invalidFilter = await admin.GetAsync("/api/v1/admin/store-activity-changes?status=1")) Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
        using (var listed = await admin.GetAsync("/api/v1/admin/store-activity-changes?status=Pending&page=1&pageSize=100")) Assert.Contains((await listed.Content.ReadFromJsonAsync<MerchantPage<ActivityChangeDto>>())!.Items, value => value.Id == request.Id);
        await using (var context = database.CreateContext()) await context.Set<ApplicationUser>().Where(value => value.Id == setup.Admin.Id).ExecuteUpdateAsync(set => set.SetProperty(value => value.AccountType, AccountType.Customer));
        using (var staleAdmin = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, true), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion))) Assert.Contains(staleAdmin.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        await using var read = database.CreateContext(); Assert.Equal(ActivityChangeStatus.Pending, (await read.Set<StoreActivityChange>().SingleAsync(value => value.Id == request.Id)).Status);
        Assert.False(await read.Set<AuditEntry>().AnyAsync(value => value.TargetId == setup.Store.Id && value.Operation == "Store.ActivityChange.Approve"));
    }
    [Fact]
    public async Task An_old_store_version_and_a_replayed_review_cannot_override_committed_data()
    {
        var setup = await SetupAsync(); using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        var request = await RequestAsync(owner, setup.Store, setup.Target);
        await using (var context = database.CreateContext()) await context.Set<Store>().Where(value => value.Id == setup.Store.Id).ExecuteUpdateAsync(set => set.SetProperty(value => value.Name, "Current name"));
        using (var stale = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, true), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion))) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var list = await admin.GetAsync("/api/v1/admin/store-activity-changes?pageSize=100")) request = (await list.Content.ReadFromJsonAsync<MerchantPage<ActivityChangeDto>>())!.Items.Single(value => value.Id == request.Id);
        using (var approved = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, true), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion))) Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using (var replay = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, false), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion, "late reject"))) Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }
    [Fact]
    public async Task Soft_deletion_blocks_review_and_final_purge_removes_requests_but_retains_store_audit()
    {
        var setup = await SetupAsync(); using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        var request = await RequestAsync(owner, setup.Store, setup.Target);
        using (var deleted = await owner.DeleteBrowserAsync("/api/v1/identity/account", new { password = setup.Owner.Password })) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var unavailable = await admin.PostBrowserJsonAsync(ReviewRoute(request.Id, true), new ActivityChangeReviewWrite(request.RowVersion, request.StoreRowVersion))) Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        Guid generation; await using (var read = database.CreateContext()) generation = (await read.Set<AccountDeletion>().SingleAsync(value => value.UserId == setup.Owner.Id)).Generation;
        clock.Advance(TimeSpan.FromDays(31)); await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AccountPurgeJob>().RunAsync(setup.Owner.Id, generation, default);
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<StoreActivityChange>().AnyAsync(value => value.Id == request.Id));
        Assert.True(await verify.Set<AuditEntry>().AnyAsync(value => value.TargetId == setup.Store.Id && value.Operation == "Store.ActivityChange.Request"));
    }
    [Fact]
    public async Task OpenAPI_exposes_only_real_versioned_routes_with_established_security()
    {
        using var client = factory.CreateHttpsClient(); using var json = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json")); var paths = json.RootElement.GetProperty("paths");
        var mutation = paths.GetProperty("/api/v1/admin/store-activity-changes/{id}/approve").GetProperty("post");
        Assert.Contains(mutation.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "X-CSRF-TOKEN" && value.GetProperty("required").GetBoolean());
        foreach (var code in new[] { "400", "401", "403", "404", "409", "429" }) Assert.True(mutation.GetProperty("responses").TryGetProperty(code, out _));
        Assert.True(paths.TryGetProperty("/api/v1/merchants/stores/{storeId}/activity-change-requests", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catalog_lock_unavailability_returns_safe_conflict_without_changing_request_or_activity(bool review)
    {
        var setup = await SetupAsync(); using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        ActivityChangeDto? change = review ? await RequestAsync(owner, setup.Store, setup.Target) : null;
        await using var blocker = database.CreateContext(); await using var transaction = await blocker.BeginAsync(); await new CatalogTreeLock(blocker).AcquireAsync(default, 0);
        using var response = review ? await admin.PostBrowserJsonAsync(ReviewRoute(change!.Id, true), new ActivityChangeReviewWrite(change.RowVersion, change.StoreRowVersion))
            : await owner.PostBrowserJsonAsync(OwnerRoute(setup.Store.Id), new ActivityChangeWrite(setup.Target, setup.Store.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("51010", json); Assert.DoesNotContain("catalog-reference-tree", json);
        await transaction.CommitAsync(); await using var read = database.CreateContext(); Assert.Equal(setup.Store.ActivityId, (await read.Set<Store>().SingleAsync(value => value.Id == setup.Store.Id)).ActivityId);
        if (review) Assert.Equal(ActivityChangeStatus.Pending, (await read.Set<StoreActivityChange>().SingleAsync(value => value.Id == change!.Id)).Status);
        else Assert.False(await read.Set<StoreActivityChange>().AnyAsync(value => value.StoreId == setup.Store.Id));
        Assert.False(await read.Set<AuditEntry>().AnyAsync(value => value.TargetId == setup.Store.Id && value.Operation == "Store.ActivityChange.Approve"));
    }
}
