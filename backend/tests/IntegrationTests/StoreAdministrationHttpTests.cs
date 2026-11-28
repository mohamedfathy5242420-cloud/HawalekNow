using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Messaging;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class StoreAdministrationHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly string root = StoreTestData.Root();
    private readonly ManualTimeProvider clock = new(TimeProvider.System.GetUtcNow());
    private IdentityApiFactory factory = null!;
    public Task InitializeAsync() { factory = new(database, clock, settings: new Dictionary<string, string?> { ["FileStorage:Root"] = root }); _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private async Task<(MerchantTestAccount Owner, MerchantTestAccount Admin, StoreDto Store, BranchDto Branch, BranchWrite Details)> SetupAsync(bool approve = true)
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        var store = await StoreTestData.CreateAsync(database, owner, new LocalFileStorage(root, clock)); var details = BranchTestData.Write(await BranchTestData.PathAsync(database)); BranchDto branch;
        await using (var context = database.CreateContext()) branch = (await BranchTestData.Service(context).CreateAsync(owner.Id, store.Id, details, default)).Value;
        if (approve)
        {
            using var administrator = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(administrator, admin);
            using var approved = await administrator.PostBrowserJsonAsync($"/api/v1/admin/merchants/{owner.ProfileId}/approve", new { rowVersion = await MerchantTestAccounts.VersionAsync(database, owner.ProfileId) }); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        }
        return (owner, admin, store, branch, details);
    }
    private static async Task<StoreDto> ModerateAsync(HttpClient admin, StoreDto store, string action)
    {
        using var response = await admin.PostBrowserJsonAsync($"/api/v1/admin/stores/{store.Id}/{action}", new StoreModerationWrite(store.RowVersion, action + " reason"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<StoreDto>())!;
    }
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string route, object value, bool csrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(value) }; if (csrf) await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, StoreDto store, byte[] bytes, string? reason = "logo correction")
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(store.RowVersion), "rowVersion"); if (reason is not null) form.Add(new StringContent(reason), "reason");
        var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue("text/plain"); form.Add(content, "file", "../../pretend.svg");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/stores/{store.Id}/logo") { Content = form }; await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    private static async Task PublicStatusAsync(HttpClient guest, Guid storeId, Guid branchId, HttpStatusCode status, bool logo = true)
    {
        var paths = new List<string> { $"stores/{storeId}", $"stores/{storeId}/branches", $"branches/{branchId}" }; if (logo) paths.Add($"stores/{storeId}/logo");
        foreach (var path in paths) { using var response = await guest.GetAsync("/api/v1/" + path); Assert.Equal(status, response.StatusCode); }
    }
    [Fact]
    public async Task Hide_and_suspend_gate_all_public_routes_and_owner_mutations_without_overriding_Identity()
    {
        var setup = await SetupAsync(); using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin); var store = setup.Store;
        using (var uploaded = await UploadAsync(admin, store, StorageTestImages.Png())) { Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); store = (await uploaded.Content.ReadFromJsonAsync<StoreDto>())!; }
        await PublicStatusAsync(guest, store.Id, setup.Branch.Id, HttpStatusCode.OK);
        store = await ModerateAsync(admin, store, "hide"); await PublicStatusAsync(guest, store.Id, setup.Branch.Id, HttpStatusCode.NotFound);
        using (var editable = await PutAsync(owner, $"/api/v1/merchants/stores/{store.Id}", new StoreUpdateWrite(store.RowVersion, StoreTestData.Details("Owner prepares hidden store"))))
        { Assert.Equal(HttpStatusCode.OK, editable.StatusCode); store = (await editable.Content.ReadFromJsonAsync<StoreDto>())!; Assert.True(store.IsHidden); }
        store = await ModerateAsync(admin, store, "suspend"); Assert.False(store.CanOwnerEdit);
        using (var privateRead = await owner.GetAsync("/api/v1/merchants/store")) { Assert.Equal(HttpStatusCode.OK, privateRead.StatusCode); Assert.Equal("suspend reason", (await privateRead.Content.ReadFromJsonAsync<StoreDto>())!.SuspensionReason); }
        using (var edit = await PutAsync(owner, $"/api/v1/merchants/stores/{store.Id}", new StoreUpdateWrite(store.RowVersion, StoreTestData.Details()))) Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        using (var branchCreate = await owner.PostBrowserJsonAsync($"/api/v1/merchants/stores/{store.Id}/branches", setup.Details)) Assert.Equal(HttpStatusCode.Forbidden, branchCreate.StatusCode);
        using (var branchEdit = await PutAsync(owner, $"/api/v1/merchants/stores/{store.Id}/branches/{setup.Branch.Id}", new BranchUpdateWrite(setup.Branch.RowVersion, setup.Details))) Assert.Equal(HttpStatusCode.Forbidden, branchEdit.StatusCode);
        using (var logoEdit = await owner.DeleteBrowserAsync($"/api/v1/merchants/stores/{store.Id}/logo", new { rowVersion = store.RowVersion })) Assert.Equal(HttpStatusCode.Forbidden, logoEdit.StatusCode);
        using (var activityRequest = await owner.PostBrowserJsonAsync($"/api/v1/merchants/stores/{store.Id}/activity-change-requests", new ActivityChangeWrite(await StoreTestData.ActivityAsync(database), store.RowVersion))) Assert.Equal(HttpStatusCode.Forbidden, activityRequest.StatusCode);
        using var freshOwner = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(freshOwner, setup.Owner);
        using (var account = await freshOwner.GetAsync("/api/v1/merchants/account/publishing-access")) Assert.Equal(HttpStatusCode.OK, account.StatusCode); // Existing account-level approval probe.
        store = await ModerateAsync(admin, store, "resume"); Assert.True(store.IsHidden); await PublicStatusAsync(guest, store.Id, setup.Branch.Id, HttpStatusCode.NotFound);
        store = await ModerateAsync(admin, store, "publish"); await PublicStatusAsync(guest, store.Id, setup.Branch.Id, HttpStatusCode.OK);
        await using var read = database.CreateContext(); Assert.Equal(MerchantApprovalStatus.Approved, (await read.Set<MerchantAccount>().SingleAsync(value => value.Id == setup.Owner.ProfileId)).Status);
    }
    [Fact]
    public async Task Publish_does_not_clear_suspension_and_cannot_approve_a_pending_merchant()
    {
        var setup = await SetupAsync(false); using var admin = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        var store = await ModerateAsync(admin, setup.Store, "hide"); store = await ModerateAsync(admin, store, "suspend"); store = await ModerateAsync(admin, store, "publish");
        Assert.True(store.IsSuspended); Assert.False(store.IsHidden); await PublicStatusAsync(guest, store.Id, setup.Branch.Id, HttpStatusCode.NotFound, false);
        store = await ModerateAsync(admin, store, "resume"); await PublicStatusAsync(guest, store.Id, setup.Branch.Id, HttpStatusCode.NotFound, false);
        await using var context = database.CreateContext(); Assert.Equal(MerchantApprovalStatus.Pending, (await context.Set<MerchantAccount>().SingleAsync(value => value.Id == setup.Owner.ProfileId)).Status);
    }
    [Fact]
    public async Task Admin_edits_and_logo_use_real_validation_versions_reasons_and_explicit_audit()
    {
        var setup = await SetupAsync(); using var admin = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(admin, setup.Admin); var store = setup.Store;
        using (var missingReason = await UploadAsync(admin, store, StorageTestImages.Png(), null)) Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        using (var badImage = await UploadAsync(admin, store, Encoding.UTF8.GetBytes("<svg/>"))) Assert.Equal(HttpStatusCode.BadRequest, badImage.StatusCode);
        using (var edited = await PutAsync(admin, $"/api/v1/admin/stores/{store.Id}", new { rowVersion = store.RowVersion, details = StoreTestData.Details("Admin corrected store"), reason = "contact correction", actorId = setup.Owner.Id, merchantAccountId = Guid.NewGuid(), activityId = Guid.NewGuid() }))
        { Assert.Equal(HttpStatusCode.OK, edited.StatusCode); store = (await edited.Content.ReadFromJsonAsync<StoreDto>())!; Assert.Equal(setup.Owner.ProfileId, store.MerchantAccountId); Assert.Equal(setup.Store.ActivityId, store.ActivityId); }
        using (var edited = await PutAsync(admin, $"/api/v1/admin/stores/{store.Id}/branches/{setup.Branch.Id}", new AdminBranchUpdateWrite(setup.Branch.RowVersion, setup.Details with { Name = "Admin corrected branch" }, "address correction"))) Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using (var uploaded = await UploadAsync(admin, store, StorageTestImages.Png(2, 3, "must-strip-this"))) { Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode); store = (await uploaded.Content.ReadFromJsonAsync<StoreDto>())!; }
        using (var logo = await admin.GetAsync($"/api/v1/admin/stores/{store.Id}/logo")) { Assert.Equal(HttpStatusCode.OK, logo.StatusCode); Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType); Assert.DoesNotContain("must-strip-this", Encoding.UTF8.GetString(await logo.Content.ReadAsByteArrayAsync())); }
        using (var removed = await admin.DeleteBrowserAsync($"/api/v1/admin/stores/{store.Id}/logo", new StoreModerationWrite(store.RowVersion, "remove outdated image"))) Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using (var audits = await admin.GetAsync($"/api/v1/admin/stores/{store.Id}/audit?pageSize=100"))
        { var rows = (await audits.Content.ReadFromJsonAsync<MerchantPage<MerchantAuditDto>>())!.Items; Assert.Contains(rows, value => value.Operation == "Store.AdminEdit" && value.ActorId == setup.Admin.Id && value.Reason == "contact correction"); Assert.Contains(rows, value => value.Operation == "Store.AdminChangeLogo"); }
        using (var audit = await admin.GetAsync($"/api/v1/admin/merchant-branches/{setup.Branch.Id}/audit")) Assert.Contains((await audit.Content.ReadFromJsonAsync<MerchantPage<MerchantAuditDto>>())!.Items, value => value.Operation == "Branch.AdminEdit" && value.Reason == "address correction");
        await using var read = database.CreateContext(); Assert.True(await read.Set<OutboxMessage>().AnyAsync(value => value.Kind == LogoDeleteHandler.KindName && value.SubjectId == setup.Owner.Id));
    }
    [Fact]
    public async Task Non_admins_other_owners_missing_CSRF_reasons_and_stale_versions_cannot_mutate_admin_data()
    {
        var setup = await SetupAsync(); var other = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); using var owner = factory.CreateHttpsClient(); using var stranger = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(stranger, other); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        using (var forbidden = await owner.PostBrowserJsonAsync($"/api/v1/admin/stores/{setup.Store.Id}/hide", new StoreModerationWrite(setup.Store.RowVersion, "reason"))) Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var stolen = await PutAsync(stranger, $"/api/v1/merchants/stores/{setup.Store.Id}", new StoreUpdateWrite(setup.Store.RowVersion, StoreTestData.Details()))) Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        using (var privateList = await stranger.GetAsync("/api/v1/admin/stores")) Assert.Equal(HttpStatusCode.Forbidden, privateList.StatusCode);
        using (var missingCsrf = await admin.PostAsJsonAsync($"/api/v1/admin/stores/{setup.Store.Id}/hide", new StoreModerationWrite(setup.Store.RowVersion, "reason"))) Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        using (var missingReason = await admin.PostBrowserJsonAsync($"/api/v1/admin/stores/{setup.Store.Id}/hide", new StoreModerationWrite(setup.Store.RowVersion, " "))) Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        var hidden = await ModerateAsync(admin, setup.Store, "hide");
        using (var stale = await admin.PostBrowserJsonAsync($"/api/v1/admin/stores/{setup.Store.Id}/suspend", new StoreModerationWrite(setup.Store.RowVersion, "reason"))) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await using (var context = database.CreateContext()) await context.Set<ApplicationUser>().Where(value => value.Id == setup.Admin.Id).ExecuteUpdateAsync(set => set.SetProperty(value => value.AccountType, AccountType.Customer));
        using var noLongerAdmin = await admin.PostBrowserJsonAsync($"/api/v1/admin/stores/{setup.Store.Id}/suspend", new StoreModerationWrite(hidden.RowVersion, "reason")); Assert.Contains(noLongerAdmin.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }
    [Fact]
    public async Task Existing_account_suspension_still_blocks_the_same_session_and_public_data()
    {
        var setup = await SetupAsync(); using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        using (var suspended = await admin.PostBrowserJsonAsync($"/api/v1/admin/merchants/{setup.Owner.ProfileId}/suspend", new MerchantReviewWrite(await MerchantTestAccounts.VersionAsync(database, setup.Owner.ProfileId), "account review"))) Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        using (var blocked = await owner.GetAsync("/api/v1/merchants/store")) Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        await PublicStatusAsync(guest, setup.Store.Id, setup.Branch.Id, HttpStatusCode.NotFound, false);
        var hidden = await ModerateAsync(admin, setup.Store, "hide"); var published = await ModerateAsync(admin, hidden, "publish"); Assert.False(published.IsSuspended);
        await PublicStatusAsync(guest, setup.Store.Id, setup.Branch.Id, HttpStatusCode.NotFound, false);
        using (var restored = await admin.PostBrowserJsonAsync($"/api/v1/admin/merchants/{setup.Owner.ProfileId}/reinstate", new MerchantReviewWrite(await MerchantTestAccounts.VersionAsync(database, setup.Owner.ProfileId), "account review resolved"))) Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        using (var access = await owner.GetAsync("/api/v1/merchants/store")) Assert.Equal(HttpStatusCode.OK, access.StatusCode);
        await PublicStatusAsync(guest, setup.Store.Id, setup.Branch.Id, HttpStatusCode.OK, false);
    }
    [Fact]
    public async Task Soft_delete_and_real_OTP_recovery_preserve_store_restrictions_and_never_restore_a_session()
    {
        var setup = await SetupAsync(); using var owner = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(owner, setup.Owner); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        var store = await ModerateAsync(admin, setup.Store, "hide"); store = await ModerateAsync(admin, store, "suspend");
        using (var deleted = await owner.DeleteBrowserAsync("/api/v1/identity/account", new { password = setup.Owner.Password })) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var unavailable = await admin.PostBrowserJsonAsync($"/api/v1/admin/stores/{store.Id}/resume", new StoreModerationWrite(store.RowVersion, "reason"))) Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        using (var requested = await owner.PostBrowserJsonAsync("/api/v1/identity/recovery/request", new { accountType = "Merchant", email = setup.Owner.Email })) Assert.Equal(HttpStatusCode.NoContent, requested.StatusCode);
        EmailOtpPayload payload;
        await using (var context = database.CreateContext())
        {
            var messages = await context.Set<OutboxMessage>().Where(value => value.Kind == EmailOtpHandler.MessageKind && value.ProtectedPayload != "").OrderByDescending(value => value.Id).ToArrayAsync();
            payload = messages.Select(value => factory.Services.GetRequiredService<PayloadProtection>().Unprotect<EmailOtpPayload>(value.Kind, value.ProtectedPayload)).First(value => value.UserId == setup.Owner.Id && value.Purpose == OtpPurpose.AccountRecovery);
        }
        using (var recovered = await owner.PostBrowserJsonAsync("/api/v1/identity/recovery/verify", new { accountType = "Merchant", email = setup.Owner.Email, code = payload.Code })) Assert.Equal(HttpStatusCode.NoContent, recovered.StatusCode);
        using (var oldSession = await owner.GetAsync("/api/v1/merchants/store")) Assert.Equal(HttpStatusCode.Unauthorized, oldSession.StatusCode);
        await MerchantTestAccounts.LoginAsync(owner, setup.Owner);
        using (var retained = await owner.GetAsync("/api/v1/merchants/store")) { var result = (await retained.Content.ReadFromJsonAsync<StoreDto>())!; Assert.True(result.IsHidden); Assert.True(result.IsSuspended); Assert.False(result.CanOwnerEdit); }
        await PublicStatusAsync(guest, store.Id, setup.Branch.Id, HttpStatusCode.NotFound, false);
    }
    [Fact]
    public async Task Administrative_OpenAPI_and_bounded_reads_have_real_security_contracts()
    {
        var setup = await SetupAsync(); using var admin = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(admin, setup.Admin);
        using (var read = await admin.GetAsync($"/api/v1/admin/stores/{setup.Store.Id}")) { var dto = (await read.Content.ReadFromJsonAsync<AdminStoreDto>())!; Assert.Equal(setup.Owner.Id, dto.OwnerUserId); }
        using (var list = await admin.GetAsync("/api/v1/admin/stores?pageSize=1")) Assert.Single((await list.Content.ReadFromJsonAsync<MerchantPage<AdminStoreDto>>())!.Items);
        using (var branches = await admin.GetAsync($"/api/v1/admin/stores/{setup.Store.Id}/branches")) Assert.Contains((await branches.Content.ReadFromJsonAsync<MerchantPage<BranchDto>>())!.Items, value => value.Id == setup.Branch.Id);
        using (var invalid = await admin.GetAsync("/api/v1/admin/stores?pageSize=101")) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var json = JsonDocument.Parse(await admin.GetStringAsync("/openapi/v1.json")); var mutation = json.RootElement.GetProperty("paths").GetProperty("/api/v1/admin/stores/{id}/hide").GetProperty("post");
        Assert.Contains(mutation.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "X-CSRF-TOKEN" && value.GetProperty("required").GetBoolean());
        foreach (var status in new[] { "400", "401", "403", "404", "409", "429" }) Assert.True(mutation.GetProperty("responses").TryGetProperty(status, out _));
    }
}
