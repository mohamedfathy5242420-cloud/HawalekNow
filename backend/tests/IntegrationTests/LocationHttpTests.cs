using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HawalekNow.Application.Modules.Locations;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class LocationHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    private HttpClient client = null!;
    public Task InitializeAsync() { client = factory.CreateHttpsClient(); return Task.CompletedTask; }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task LoginAsync(AccountType type = AccountType.Admin)
    {
        using var scope = factory.Services.CreateScope(); var name = "location" + Guid.NewGuid().ToString("N"); var password = "Test-only-" + Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(type, name, name + "@example.invalid"); user.EmailConfirmed = true;
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded);
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = type.ToString(), identifier = user.Email, password }); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private async Task<LocationAdminDto> CreateAsync(string collection, Guid? governorate = null, Guid? city = null, bool active = true)
    {
        using var response = await client.PostBrowserJsonAsync("/api/v1/admin/locations/" + collection, new LocationWrite("تجربة " + Guid.NewGuid(), "Review " + Guid.NewGuid(), active, governorate, city));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<LocationAdminDto>())!;
    }
    private async Task<HttpResponseMessage> PutAsync(string collection, LocationAdminDto place, LocationWrite value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/admin/locations/" + collection + "/" + place.Id) { Content = JsonContent.Create(value) };
        await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "code", "correlationId", "status", "title", "type" }, body.RootElement.EnumerateObject().Select(value => value.Name).Order().ToArray());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task All_three_admin_resources_reject_guests_and_regular_users(bool login, HttpStatusCode expected)
    {
        if (login) await LoginAsync(AccountType.Customer);
        foreach (var collection in new[] { "governorates", "cities", "areas" })
        {
            var governorate = Guid.NewGuid(); var city = Guid.NewGuid(); var id = Guid.NewGuid();
            var path = "/api/v1/admin/locations/" + collection;
            using (var read = await client.GetAsync(path + $"?governorateId={governorate}&cityId={city}")) await ProblemAsync(read, expected);
            var body = new LocationWrite("اسم", "Name", true, collection == "governorates" ? null : governorate, collection == "areas" ? city : null, "AAAAAAAAAAA=");
            using (var create = await client.PostBrowserJsonAsync(path, body)) await ProblemAsync(create, expected);
            using (var update = await PutAsync(collection, new(id, "", "", true, null, null, ""), body)) await ProblemAsync(update, expected);
            using (var delete = await client.DeleteBrowserAsync(path + "/" + id, new LocationDelete("AAAAAAAAAAA="))) await ProblemAsync(delete, expected);
        }
    }

    [Fact]
    public async Task Crud_rejects_wrong_pairs_referenced_deletes_and_lost_updates()
    {
        await LoginAsync(); var first = await CreateAsync("governorates"); var second = await CreateAsync("governorates");
        var city = await CreateAsync("cities", first.Id); var area = await CreateAsync("areas", first.Id, city.Id);
        using (var wrong = await client.PostBrowserJsonAsync("/api/v1/admin/locations/areas", new LocationWrite("غيرصالح", "Invalid", true, second.Id, city.Id))) await ProblemAsync(wrong, HttpStatusCode.BadRequest);
        using (var move = await PutAsync("cities", city, new(city.NameAr, city.NameEn, true, second.Id, RowVersion: city.RowVersion))) await ProblemAsync(move, HttpStatusCode.Conflict);
        foreach (var (collection, place) in new[] { ("governorates", first), ("cities", city) })
        { using var delete = await client.DeleteBrowserAsync("/api/v1/admin/locations/" + collection + "/" + place.Id, new LocationDelete(place.RowVersion)); await ProblemAsync(delete, HttpStatusCode.Conflict); }
        LocationAdminDto changed;
        using (var rename = await PutAsync("areas", area, new("اسم جديد", "Changed", true, first.Id, city.Id, area.RowVersion)))
        { Assert.Equal(HttpStatusCode.OK, rename.StatusCode); changed = (await rename.Content.ReadFromJsonAsync<LocationAdminDto>())!; }
        using (var stale = await PutAsync("areas", area, new("ضائع", "Lost", false, first.Id, city.Id, area.RowVersion))) await ProblemAsync(stale, HttpStatusCode.Conflict);
        var rows = await client.GetFromJsonAsync<LocationPage<LocationAdminDto>>($"/api/v1/admin/locations/areas?governorateId={first.Id}&cityId={city.Id}");
        Assert.Single(rows!.Items); Assert.Equal("Changed", rows.Items[0].NameEn); Assert.True(rows.Items[0].IsActive);
        foreach (var (collection, place) in new[] { ("areas", changed), ("cities", city), ("governorates", first) })
        { using var delete = await client.DeleteBrowserAsync("/api/v1/admin/locations/" + collection + "/" + place.Id, new LocationDelete(place.RowVersion)); Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode); }
    }

    [Fact]
    public async Task Visitor_reads_follow_all_active_ancestors_and_do_not_expose_admin_fields()
    {
        await LoginAsync(); var governorate = await CreateAsync("governorates"); var city = await CreateAsync("cities", governorate.Id);
        var area = await CreateAsync("areas", governorate.Id, city.Id); _ = await CreateAsync("areas", governorate.Id, city.Id, false);
        using var visitor = factory.CreateHttpsClient();
        var route = $"/api/v1/locations/areas?governorateId={governorate.Id}&cityId={city.Id}";
        var original = await visitor.GetFromJsonAsync<LocationPage<LocationOption>>(route + "&language=en"); Assert.Single(original!.Items); Assert.Equal(area.NameEn, original.Items[0].Name);
        using (var disabled = await PutAsync("cities", city, new(city.NameAr, city.NameEn, false, governorate.Id, RowVersion: city.RowVersion)))
        { Assert.Equal(HttpStatusCode.OK, disabled.StatusCode); city = (await disabled.Content.ReadFromJsonAsync<LocationAdminDto>())!; }
        Assert.Empty((await visitor.GetFromJsonAsync<LocationPage<LocationOption>>(route))!.Items);
        using (var enabled = await PutAsync("cities", city, new(city.NameAr, city.NameEn, true, governorate.Id, RowVersion: city.RowVersion))) Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        using (var disabled = await PutAsync("governorates", governorate, new(governorate.NameAr, governorate.NameEn, false, RowVersion: governorate.RowVersion)))
        { Assert.Equal(HttpStatusCode.OK, disabled.StatusCode); governorate = (await disabled.Content.ReadFromJsonAsync<LocationAdminDto>())!; }
        Assert.Empty((await visitor.GetFromJsonAsync<LocationPage<LocationOption>>(route))!.Items);
        Assert.Empty((await visitor.GetFromJsonAsync<LocationPage<LocationOption>>($"/api/v1/locations/cities?governorateId={governorate.Id}"))!.Items);
        using (var enabled = await PutAsync("governorates", governorate, new(governorate.NameAr, governorate.NameEn, true, RowVersion: governorate.RowVersion))) Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        using var response = await visitor.GetAsync(route + "&language=ar"); var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("rowVersion", json); Assert.DoesNotContain("isActive", json); Assert.DoesNotContain("nameEn", json);
        Assert.Single((await response.Content.ReadFromJsonAsync<LocationPage<LocationOption>>())!.Items);
    }

    [Fact]
    public async Task Public_selection_requires_correct_parents_and_bounded_stable_pages()
    {
        await LoginAsync(); var governorate = await CreateAsync("governorates");
        var first = await CreateAsync("cities", governorate.Id); var second = await CreateAsync("cities", governorate.Id); _ = await CreateAsync("areas", governorate.Id, first.Id);
        using var visitor = factory.CreateHttpsClient(); var route = $"/api/v1/locations/cities?governorateId={governorate.Id}&pageSize=1";
        var one = await visitor.GetFromJsonAsync<LocationPage<LocationOption>>(route); var two = await visitor.GetFromJsonAsync<LocationPage<LocationOption>>(route + "&page=2");
        Assert.True(one!.HasMore); Assert.False(two!.HasMore); Assert.NotEqual(one.Items[0].Id, two.Items[0].Id);
        Assert.Equal(one.Items[0].Id, (await visitor.GetFromJsonAsync<LocationPage<LocationOption>>(route))!.Items[0].Id);
        foreach (var bad in new[] { "/api/v1/locations/cities", "/api/v1/locations/governorates?pageSize=101", "/api/v1/locations/governorates?page=0", "/api/v1/locations/governorates?language=fr", $"/api/v1/locations/areas?governorateId={governorate.Id}" })
        { using var response = await visitor.GetAsync(bad); await ProblemAsync(response, HttpStatusCode.BadRequest); }
        var wrongPair = await visitor.GetFromJsonAsync<LocationPage<LocationOption>>($"/api/v1/locations/areas?governorateId={Guid.NewGuid()}&cityId={second.Id}"); Assert.Empty(wrongPair!.Items);
        using var rejected = await visitor.PostBrowserJsonAsync("/api/v1/locations/governorates", new LocationWrite("ممنوع", "Forbidden", true)); Assert.Equal(HttpStatusCode.MethodNotAllowed, rejected.StatusCode);
    }

    [Fact]
    public async Task Location_writes_require_csrf_https_and_the_shared_reference_quota()
    {
        await LoginAsync();
        using (var missing = await client.PostAsJsonAsync("/api/v1/admin/locations/governorates", new LocationWrite("اسم", "Name", true))) await ProblemAsync(missing, HttpStatusCode.BadRequest);
        using (var invalid = await client.PostBrowserJsonAsync("/api/v1/admin/locations/governorates", new LocationWrite("اسم", "", true))) await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/admin/locations/governorates") { Content = JsonContent.Create(new LocationWrite("اسم", "Name", true)) })
        { await BrowserRequests.AddCsrfAsync(client, request); using var response = await client.SendAsync(request); Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }); }
        // Two previous writes consumed this peer's reference quota; HTTP without the secure cookie may consume another.
        HttpResponseMessage? limited = null;
        try
        {
            for (var i = 0; i < 61; i++)
            {
                var response = await client.PostAsJsonAsync(i % 2 == 0 ? "/api/v1/admin/locations/governorates" : "/api/v1/admin/catalog/activities", new { nameAr = "اسم", nameEn = "Name", isActive = true });
                if (response.StatusCode == HttpStatusCode.TooManyRequests) { limited = response; break; }
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); response.Dispose();
            }
            Assert.NotNull(limited); await ProblemAsync(limited, HttpStatusCode.TooManyRequests); Assert.NotNull(limited.Headers.RetryAfter);
        }
        finally { limited?.Dispose(); }
    }
}
