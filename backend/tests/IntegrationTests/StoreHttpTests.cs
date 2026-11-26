using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Storage;
using HawalekNow.Domain.Modules.Catalog;
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

public sealed class StoreHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly string root = StoreTestData.Root();
    private readonly ManualTimeProvider clock = new(TimeProvider.System.GetUtcNow());
    private IdentityApiFactory factory = null!;
    public Task InitializeAsync()
    {
        factory = new(database, clock, settings: new Dictionary<string, string?> { ["FileStorage:Root"] = root }); _ = factory.Services; return Task.CompletedTask;
    }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private async Task<StoreDto> CreateAsync(HttpClient client, Guid? activityId = null)
    {
        var activity = activityId ?? await StoreTestData.ActivityAsync(database);
        using var response = await client.PostBrowserJsonAsync("/api/v1/merchants/store", new { activityId = activity, details = StoreTestData.Details(), merchantAccountId = Guid.NewGuid(), actorId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<StoreDto>())!;
    }
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string route, object value, bool csrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(value) };
        if (csrf) await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid id, string version, byte[] bytes, string filename = "../../pretend.svg", string mime = "text/plain", bool csrf = true)
    {
        using var form = new MultipartFormDataContent(); form.Add(new StringContent(version), "rowVersion");
        var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(mime); form.Add(file, "file", filename);
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/merchants/stores/{id}/logo") { Content = form };
        if (csrf) await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }

    [Fact]
    public async Task Pending_owner_prepares_store_and_logo_then_approval_makes_the_same_data_public()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        using var merchant = factory.CreateHttpsClient(); using var administrator = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(merchant, owner); var store = await CreateAsync(merchant); Assert.Equal(owner.ProfileId, store.MerchantAccountId); Assert.False(store.HasLogo);
        using (var response = await guest.GetAsync($"/api/v1/stores/{store.Id}")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var response = await UploadAsync(merchant, store.Id, store.RowVersion, StorageTestImages.Png(2, 3, "private-geo-marker")))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); store = (await response.Content.ReadFromJsonAsync<StoreDto>())!; Assert.True(store.HasLogo);
            var json = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("logoKey", json); Assert.DoesNotContain(".png", json); Assert.DoesNotContain(root, json);
        }
        using (var response = await merchant.GetAsync($"/api/v1/merchants/stores/{store.Id}/logo"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.DoesNotContain("private-geo-marker", Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()));
        }
        using (var response = await guest.GetAsync($"/api/v1/stores/{store.Id}/logo")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await MerchantTestAccounts.LoginAsync(administrator, admin);
        using (var response = await administrator.PostBrowserJsonAsync($"/api/v1/admin/merchants/{owner.ProfileId}/approve", new { rowVersion = await MerchantTestAccounts.VersionAsync(database, owner.ProfileId) })) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var response = await guest.GetAsync($"/api/v1/stores/{store.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal("متجر الاختبار", json.RootElement.GetProperty("name").GetString());
            Assert.False(json.RootElement.TryGetProperty("rowVersion", out _)); Assert.False(json.RootElement.TryGetProperty("merchantAccountId", out _));
        }
        using (var response = await guest.GetAsync($"/api/v1/stores/{store.Id}/logo"))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.NoStore); Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        }
        await using var read = database.CreateContext(); var stored = await read.Set<Store>().SingleAsync(value => value.Id == store.Id);
        Assert.NotNull(await new LocalFileStorage(root, clock).ReadImageAsync(stored.LogoKey!, default));
    }

    [Fact]
    public async Task Other_merchant_cannot_edit_or_read_private_logo_and_cannot_reassign_owner_or_activity()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var other = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        using var first = factory.CreateHttpsClient(); using var second = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(first, owner); await MerchantTestAccounts.LoginAsync(second, other); var store = await CreateAsync(first);
        using (var logo = await UploadAsync(first, store.Id, store.RowVersion, StorageTestImages.Png()))
        {
            Assert.Equal(HttpStatusCode.OK, logo.StatusCode); store = (await logo.Content.ReadFromJsonAsync<StoreDto>())!;
        }
        using (var response = await PutAsync(second, $"/api/v1/merchants/stores/{store.Id}", new StoreUpdateWrite(store.RowVersion, StoreTestData.Details("forged")))) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var response = await UploadAsync(second, store.Id, store.RowVersion, StorageTestImages.Png())) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using (var response = await second.GetAsync($"/api/v1/merchants/stores/{store.Id}/logo")) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var otherActivity = await StoreTestData.ActivityAsync(database);
        using (var response = await PutAsync(first, $"/api/v1/merchants/stores/{store.Id}", new { rowVersion = store.RowVersion, details = StoreTestData.Details("Edited"), activityId = otherActivity, merchantAccountId = other.ProfileId }))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode); var updated = (await response.Content.ReadFromJsonAsync<StoreDto>())!;
            Assert.Equal(store.ActivityId, updated.ActivityId); Assert.Equal(owner.ProfileId, updated.MerchantAccountId);
        }
        using (var response = await first.PostBrowserJsonAsync("/api/v1/merchants/store", new StoreCreateWrite(otherActivity, StoreTestData.Details()))) Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var read = database.CreateContext(); Assert.All(await read.Set<AuditEntry>().Where(value => value.TargetId == store.Id).ToArrayAsync(), value => Assert.Equal(owner.Id, value.ActorId));
    }

    [Fact]
    public async Task Actual_requests_enforce_HTTPS_CSRF_content_limits_versions_and_single_text_name()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); using var client = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(client, owner);
        var store = await CreateAsync(client); var route = $"/api/v1/merchants/stores/{store.Id}";
        using (var response = await PutAsync(client, route, new StoreUpdateWrite(store.RowVersion, StoreTestData.Details()), false)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // A real browser withholds its Secure cookie on HTTP, so authentication rejects it first.
        using (var response = await PutAsync(client, "http://localhost" + route, new StoreUpdateWrite(store.RowVersion, StoreTestData.Details()))) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        // The established authentication handler also ignores valid cookies manually forwarded on HTTP.
        using (var login = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = "Merchant", identifier = owner.Email, password = owner.Password }))
        {
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            var cookies = string.Join("; ", login.Headers.GetValues("Set-Cookie").Select(value => value.Split(';')[0]));
            using var request = new HttpRequestMessage(HttpMethod.Put, "http://localhost" + route) { Content = JsonContent.Create(new StoreUpdateWrite(store.RowVersion, StoreTestData.Details())) };
            await BrowserRequests.AddCsrfAsync(client, request, cookies);
            using var response = await client.SendAsync(request); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        using (var response = await UploadAsync(client, store.Id, store.RowVersion, StorageTestImages.Png(), csrf: false)) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var response = await UploadAsync(client, store.Id, store.RowVersion, Encoding.UTF8.GetBytes("<svg><script>not-image</script></svg>"), "pretend.png", "image/png")) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var response = await UploadAsync(client, store.Id, store.RowVersion, new byte[ImageStorageLimits.MaxInputBytes + 1])) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var response = await UploadAsync(client, store.Id, "invalid-version", StorageTestImages.Png())) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var response = await PutAsync(client, route, new { rowVersion = store.RowVersion, details = new { nameAr = "اسم", nameEn = "Name", phone = "+201012345678" } })) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var response = await PutAsync(client, route, new StoreUpdateWrite(store.RowVersion, new("Name")))) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using (var response = await UploadAsync(client, store.Id, store.RowVersion, StorageTestImages.Png())) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (var response = await UploadAsync(client, store.Id, store.RowVersion, StorageTestImages.Png())) Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using (var response = await client.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("Guest", true, 401)]
    [InlineData("Customer", true, 403)]
    [InlineData("Admin", true, 403)]
    [InlineData("Merchant", false, 403)]
    public async Task Setup_policy_uses_live_account_facts(string type, bool confirmed, int status)
    {
        using var client = factory.CreateHttpsClient();
        if (type != "Guest") await MerchantTestAccounts.LoginAsync(client, await MerchantTestAccounts.CreateAsync(factory, Enum.Parse<AccountType>(type), confirmed));
        using var read = await client.GetAsync("/api/v1/merchants/store"); Assert.Equal((HttpStatusCode)status, read.StatusCode);
        using var create = await client.PostBrowserJsonAsync("/api/v1/merchants/store", new StoreCreateWrite(await StoreTestData.ActivityAsync(database), StoreTestData.Details())); Assert.Equal((HttpStatusCode)status, create.StatusCode);
    }

    [Fact]
    public async Task Admin_reference_delete_returns_conflict_and_unsupported_activity_selection_does_not_create_a_store()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        using var client = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(client, owner);
        using (var invalid = await client.PostBrowserJsonAsync("/api/v1/merchants/store", new StoreCreateWrite(await StoreTestData.ActivityAsync(database, false), StoreTestData.Details()))) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var store = await CreateAsync(client); using var administrator = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(administrator, admin);
        await using var context = database.CreateContext(); var activity = await context.Set<Activity>().AsNoTracking().SingleAsync(value => value.Id == store.ActivityId);
        using var deleted = await administrator.DeleteBrowserAsync($"/api/v1/admin/catalog/activities/{activity.Id}", new { rowVersion = Convert.ToBase64String(activity.RowVersion) }); Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
    }

    [Fact]
    public async Task Storage_failure_returns_safe_503_without_changing_the_reference_or_breaking_Identity()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); using var client = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(client, owner); var store = await CreateAsync(client);
        var foreign = StoreTestData.Root(); Directory.CreateDirectory(foreign); var keep = Path.Combine(foreign, "private.txt"); await File.WriteAllTextAsync(keep, "foreign-keep");
        await using var unavailable = new IdentityApiFactory(database, clock, settings: new Dictionary<string, string?> { ["FileStorage:Root"] = foreign }); using var otherHost = unavailable.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(otherHost, owner);
        using (var response = await UploadAsync(otherHost, store.Id, store.RowVersion, StorageTestImages.Png()))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("service.unavailable", json.RootElement.GetProperty("code").GetString()); Assert.DoesNotContain(foreign, json.RootElement.GetRawText());
        }
        Assert.Equal("foreign-keep", await File.ReadAllTextAsync(keep));
        using (var response = await otherHost.GetAsync("/api/v1/identity/me")) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var read = database.CreateContext(); var after = await read.Set<Store>().SingleAsync(value => value.Id == store.Id); Assert.Null(after.LogoKey); Assert.Equal(store.RowVersion, Convert.ToBase64String(after.RowVersion));
    }

    [Fact]
    public async Task Real_Identity_soft_delete_hides_the_store_and_expiry_purge_queues_file_cleanup_atomically()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        using var client = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient(); using var administrator = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(client, owner); var store = await CreateAsync(client);
        using (var logo = await UploadAsync(client, store.Id, store.RowVersion, StorageTestImages.Png())) Assert.Equal(HttpStatusCode.OK, logo.StatusCode);
        await MerchantTestAccounts.LoginAsync(administrator, admin);
        using (var approved = await administrator.PostBrowserJsonAsync($"/api/v1/admin/merchants/{owner.ProfileId}/approve", new { rowVersion = await MerchantTestAccounts.VersionAsync(database, owner.ProfileId) })) Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using (var publicRead = await guest.GetAsync($"/api/v1/stores/{store.Id}")) Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode);
        using (var deleted = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = owner.Password })) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var hidden = await guest.GetAsync($"/api/v1/stores/{store.Id}")) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var hiddenLogo = await guest.GetAsync($"/api/v1/stores/{store.Id}/logo")) Assert.Equal(HttpStatusCode.NotFound, hiddenLogo.StatusCode);
        Guid generation; string key;
        await using (var context = database.CreateContext()) { generation = (await context.Set<AccountDeletion>().SingleAsync(value => value.UserId == owner.Id)).Generation; key = (await context.Set<Store>().SingleAsync(value => value.Id == store.Id)).LogoKey!; }
        clock.Advance(TimeSpan.FromDays(31));
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AccountPurgeJob>().RunAsync(owner.Id, generation, default);
        await using (var context = database.CreateContext())
        {
            Assert.False(await context.Set<Store>().AnyAsync(value => value.Id == store.Id)); Assert.False(await context.Set<MerchantAccount>().AnyAsync(value => value.Id == owner.ProfileId)); Assert.False(await context.Set<ApplicationUser>().AnyAsync(value => value.Id == owner.Id));
            Assert.True(await context.Set<AuditEntry>().AnyAsync(value => value.TargetId == store.Id));
            Assert.True(await context.Set<OutboxMessage>().AnyAsync(value => value.Kind == LogoDeleteHandler.KindName && value.SubjectId == owner.Id && value.State == OutboxState.Pending));
        }
        Assert.NotNull(await new LocalFileStorage(root, clock).ReadImageAsync(key, default));
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<OutboxPump>().RunAsync(default);
        Assert.Null(await new LocalFileStorage(root, clock).ReadImageAsync(key, default));
    }

    [Fact]
    public async Task OpenAPI_describes_real_binary_and_multipart_contracts_and_csrf()
    {
        using var client = factory.CreateHttpsClient(); using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json")); var paths = document.RootElement.GetProperty("paths");
        var upload = paths.GetProperty("/api/v1/merchants/stores/{id}/logo").GetProperty("put");
        Assert.True(upload.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _));
        Assert.Contains(upload.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "X-CSRF-TOKEN" && value.GetProperty("required").GetBoolean());
        var image = paths.GetProperty("/api/v1/stores/{id}/logo").GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("image/png").GetProperty("schema");
        Assert.Equal("string", image.GetProperty("type").GetString()); Assert.Equal("binary", image.GetProperty("format").GetString());
    }
}
