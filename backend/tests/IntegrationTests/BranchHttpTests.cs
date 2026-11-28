using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Modules.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class BranchHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly ManualTimeProvider clock = new(TimeProvider.System.GetUtcNow());
    private IdentityApiFactory factory = null!;
    public Task InitializeAsync() { factory = new(database, clock); _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private async Task<StoreDto> StoreAsync(HttpClient client)
    {
        using var response = await client.PostBrowserJsonAsync("/api/v1/merchants/store", new StoreCreateWrite(await StoreTestData.ActivityAsync(database), new("متجر", "+201012345678", "+201112345678")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<StoreDto>())!;
    }
    private static async Task<BranchDto> CreateAsync(HttpClient client, Guid storeId, BranchWrite value)
    {
        using var response = await client.PostBrowserJsonAsync($"/api/v1/merchants/stores/{storeId}/branches", value);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<BranchDto>())!;
    }
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string route, object value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(value) }; await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    private async Task ApproveAsync(HttpClient admin, MerchantTestAccount owner)
    {
        using var approved = await admin.PostBrowserJsonAsync($"/api/v1/admin/merchants/{owner.ProfileId}/approve", new { rowVersion = await MerchantTestAccounts.VersionAsync(database, owner.ProfileId) }); Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
    }

    [Fact]
    public async Task Multiple_branches_and_pickups_follow_merchant_approval_without_a_separate_review_and_contacts_fall_back_per_channel()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        using var client = factory.CreateHttpsClient(); using var administrator = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(client, owner); await MerchantTestAccounts.LoginAsync(administrator, admin);
        var store = await StoreAsync(client); var path = await BranchTestData.PathAsync(database);
        var first = await CreateAsync(client, store.Id, BranchTestData.Write(path));
        var pickup = await CreateAsync(client, store.Id, BranchTestData.Write(path, "PickupPoint", "+201212345678"));
        Assert.Equal("+201012345678", first.EffectivePhone); Assert.Equal("+201112345678", first.EffectiveWhatsApp); Assert.Null(first.Phone); Assert.Null(first.WhatsApp);
        Assert.Equal("+201212345678", pickup.EffectivePhone); Assert.Equal("+201112345678", pickup.EffectiveWhatsApp);
        using (var privateRead = await guest.GetAsync($"/api/v1/branches/{first.Id}")) Assert.Equal(HttpStatusCode.NotFound, privateRead.StatusCode);
        await ApproveAsync(administrator, owner);
        var next = await CreateAsync(client, store.Id, BranchTestData.Write(path, "PickupPoint", whatsApp: "+201512345678"));
        using (var publicRead = await guest.GetAsync($"/api/v1/branches/{next.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode); Assert.True(publicRead.Headers.CacheControl?.NoStore);
            var value = (await publicRead.Content.ReadFromJsonAsync<PublicBranchDto>())!; Assert.Equal("+201012345678", value.Phone); Assert.Equal("+201512345678", value.WhatsApp);
            Assert.Equal(30.0444, value.Latitude); Assert.Equal(31.2357, value.Longitude);
            using var json = JsonDocument.Parse(await publicRead.Content.ReadAsStringAsync()); Assert.False(json.RootElement.TryGetProperty("rowVersion", out _));
        }
        using (var response = await guest.GetAsync($"/api/v1/stores/{store.Id}/branches?pageSize=2"))
        {
            var page = (await response.Content.ReadFromJsonAsync<MerchantPage<PublicBranchDto>>())!; Assert.Equal(2, page.Items.Length); Assert.True(page.HasMore);
        }
        using (var response = await client.GetAsync($"/api/v1/merchants/stores/{store.Id}/branches?page=2&pageSize=2"))
        {
            var page = (await response.Content.ReadFromJsonAsync<MerchantPage<BranchDto>>())!; Assert.Single(page.Items); Assert.False(page.HasMore);
        }
        await using var read = database.CreateContext(); Assert.Equal(3, await read.Set<MerchantBranch>().CountAsync(value => value.StoreId == store.Id));
    }

    [Fact]
    public async Task Other_merchant_cannot_read_edit_create_or_move_places_under_another_store()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var other = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        using var first = factory.CreateHttpsClient(); using var second = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(first, owner); await MerchantTestAccounts.LoginAsync(second, other);
        var store = await StoreAsync(first); var otherStore = await StoreAsync(second); var path = await BranchTestData.PathAsync(database); var branch = await CreateAsync(first, store.Id, BranchTestData.Write(path));
        using (var read = await second.GetAsync($"/api/v1/merchants/stores/{store.Id}/branches")) Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using (var create = await second.PostBrowserJsonAsync($"/api/v1/merchants/stores/{store.Id}/branches", BranchTestData.Write(path))) Assert.Equal(HttpStatusCode.NotFound, create.StatusCode);
        using (var edit = await PutAsync(second, $"/api/v1/merchants/stores/{store.Id}/branches/{branch.Id}", new BranchUpdateWrite(branch.RowVersion, BranchTestData.Write(path)))) Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        using (var move = await PutAsync(second, $"/api/v1/merchants/stores/{otherStore.Id}/branches/{branch.Id}", new { rowVersion = branch.RowVersion, details = BranchTestData.Write(path), storeId = otherStore.Id, actorId = owner.Id })) Assert.Equal(HttpStatusCode.NotFound, move.StatusCode);
        await using var context = database.CreateContext(); Assert.Equal(store.Id, (await context.Set<MerchantBranch>().SingleAsync(value => value.Id == branch.Id)).StoreId);
        Assert.Single(await context.Set<AuditEntry>().Where(value => value.TargetId == branch.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Validation_requires_real_hierarchy_coordinates_public_address_and_current_versions_and_CSRF()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); using var client = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(client, owner);
        var store = await StoreAsync(client); var path = await BranchTestData.PathAsync(database); var other = await BranchTestData.PathAsync(database); var route = $"/api/v1/merchants/stores/{store.Id}/branches";
        foreach (var input in new[] { BranchTestData.Write(path) with { Latitude = null }, BranchTestData.Write(path) with { Longitude = 181 },
            BranchTestData.Write(path) with { PublicFixedAddressConfirmed = false }, BranchTestData.Write(path) with { Kind = "Pending" }, BranchTestData.Write(path) with { CityId = other.City } })
        {
            using var response = await client.PostBrowserJsonAsync(route, input); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using (var noCsrf = await client.PostAsJsonAsync(route, BranchTestData.Write(path))) Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var branch = await CreateAsync(client, store.Id, BranchTestData.Write(path));
        using (var edit = await PutAsync(client, route + "/" + branch.Id, new { rowVersion = branch.RowVersion, details = BranchTestData.Write(path) with { Name = "Edited" }, storeId = Guid.NewGuid() })) Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        using (var stale = await PutAsync(client, route + "/" + branch.Id, new BranchUpdateWrite(branch.RowVersion, BranchTestData.Write(path)))) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var invalid = await PutAsync(client, route + "/" + branch.Id, new BranchUpdateWrite("invalid-version", BranchTestData.Write(path)))) Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_delete_or_reparent_an_area_in_use_but_can_still_reparent_an_unused_area()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        using var client = factory.CreateHttpsClient(); using var administrator = factory.CreateHttpsClient(); await MerchantTestAccounts.LoginAsync(client, owner); await MerchantTestAccounts.LoginAsync(administrator, admin);
        var store = await StoreAsync(client); var first = await BranchTestData.PathAsync(database); var second = await BranchTestData.PathAsync(database); await CreateAsync(client, store.Id, BranchTestData.Write(first));
        await using var context = database.CreateContext(); var area = await context.Set<Area>().AsNoTracking().SingleAsync(value => value.Id == first.Area);
        using (var deleted = await administrator.DeleteBrowserAsync($"/api/v1/admin/locations/areas/{first.Area}", new { rowVersion = Convert.ToBase64String(area.RowVersion) })) Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        using (var moved = await PutAsync(administrator, $"/api/v1/admin/locations/areas/{first.Area}", new { nameAr = area.NameAr, nameEn = area.NameEn, isActive = true, governorateId = second.Governorate, cityId = second.City, rowVersion = Convert.ToBase64String(area.RowVersion) })) Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        var unused = await context.Set<Area>().AsNoTracking().SingleAsync(value => value.Id == second.Area);
        using var allowed = await PutAsync(administrator, $"/api/v1/admin/locations/areas/{second.Area}", new { nameAr = unused.NameAr, nameEn = unused.NameEn, isActive = true, governorateId = first.Governorate, cityId = first.City, rowVersion = Convert.ToBase64String(unused.RowVersion) }); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Soft_deletion_hides_places_and_final_purge_cascades_them_while_retaining_audit()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        using var client = factory.CreateHttpsClient(); using var administrator = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(client, owner); await MerchantTestAccounts.LoginAsync(administrator, admin);
        var store = await StoreAsync(client); var branch = await CreateAsync(client, store.Id, BranchTestData.Write(await BranchTestData.PathAsync(database), "PickupPoint")); await ApproveAsync(administrator, owner);
        using (var visible = await guest.GetAsync($"/api/v1/branches/{branch.Id}")) Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        using (var deleted = await client.DeleteBrowserAsync("/api/v1/identity/account", new { password = owner.Password })) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var hidden = await guest.GetAsync($"/api/v1/branches/{branch.Id}")) Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Guid generation; await using (var read = database.CreateContext()) { generation = (await read.Set<AccountDeletion>().SingleAsync(value => value.UserId == owner.Id)).Generation; Assert.True(await read.Set<MerchantBranch>().AnyAsync(value => value.Id == branch.Id)); }
        clock.Advance(TimeSpan.FromDays(31)); await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AccountPurgeJob>().RunAsync(owner.Id, generation, default);
        await using var verify = database.CreateContext(); Assert.False(await verify.Set<MerchantBranch>().AnyAsync(value => value.Id == branch.Id)); Assert.True(await verify.Set<AuditEntry>().AnyAsync(value => value.TargetId == branch.Id));
    }

    [Fact]
    public async Task OpenAPI_routes_are_real_and_report_the_established_security_contracts()
    {
        using var client = factory.CreateHttpsClient(); using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json")); var paths = document.RootElement.GetProperty("paths");
        var create = paths.GetProperty("/api/v1/merchants/stores/{storeId}/branches").GetProperty("post");
        Assert.Contains(create.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "X-CSRF-TOKEN" && value.GetProperty("required").GetBoolean());
        foreach (var status in new[] { "400", "401", "403", "404", "409", "429" }) Assert.True(create.GetProperty("responses").TryGetProperty(status, out _));
        Assert.True(paths.TryGetProperty("/api/v1/branches/{id}", out _)); Assert.True(paths.TryGetProperty("/api/v1/stores/{storeId}/branches", out _));
    }
}
