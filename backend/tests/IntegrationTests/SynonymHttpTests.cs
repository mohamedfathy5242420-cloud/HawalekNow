using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HawalekNow.Application.Modules.Search;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class SynonymHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private const string Route = "/api/v1/admin/search/synonym-groups";
    private readonly IdentityApiFactory factory = new(database);
    private HttpClient client = null!;
    public Task InitializeAsync() { client = factory.CreateHttpsClient(); return Task.CompletedTask; }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task LoginAsync(AccountType type = AccountType.Admin)
    {
        using var scope = factory.Services.CreateScope(); var name = "synonym" + Guid.NewGuid().ToString("N"); var password = "Test-only-" + Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(type, name, name + "@example.invalid"); user.EmailConfirmed = true;
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded);
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = type.ToString(), identifier = user.Email, password }); Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private async Task<HttpResponseMessage> PutAsync(Guid id, SynonymWrite value)
    { using var request = new HttpRequestMessage(HttpMethod.Put, Route + "/" + id) { Content = JsonContent.Create(value) }; await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request); }
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.Equal(new[] { "code", "correlationId", "status", "title", "type" }, json.RootElement.EnumerateObject().Select(value => value.Name).Order().ToArray());
    }
    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task All_admin_operations_reject_guests_and_customers(bool login, HttpStatusCode status)
    {
        if (login) await LoginAsync(AccountType.Customer); var value = new SynonymWrite("اسم", "Name", true, ["term"], "AAAAAAAAAAA=");
        using (var read = await client.GetAsync(Route)) await ProblemAsync(read, status);
        using (var create = await client.PostBrowserJsonAsync(Route, value)) await ProblemAsync(create, status);
        using (var update = await PutAsync(Guid.NewGuid(), value)) await ProblemAsync(update, status);
        using (var delete = await client.DeleteBrowserAsync(Route + "/" + Guid.NewGuid(), new SynonymDelete(value.RowVersion!))) await ProblemAsync(delete, status);
    }
    [Fact]
    public async Task Admin_crud_preserves_originals_and_rejects_duplicates_stale_writes_and_invalid_pages()
    {
        await LoginAsync(); var suffix = Guid.NewGuid().ToString("N"); var value = new SynonymWrite("ملابس", "Clothing", true, ["تيشرت" + suffix, "تي شيرت" + suffix, " T-shirt " + suffix, "Tshirt" + suffix]);
        SynonymDto created;
        using (var response = await client.PostBrowserJsonAsync(Route, value)) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); created = (await response.Content.ReadFromJsonAsync<SynonymDto>())!; Assert.Equal(value.Terms.ToArray(), created.Terms.ToArray()); }
        using (var duplicate = await client.PostBrowserJsonAsync(Route, value with { Terms = [value.Terms[3].ToUpperInvariant()] })) await ProblemAsync(duplicate, HttpStatusCode.Conflict);
        using (var invalid = await client.PostBrowserJsonAsync(Route, value with { Terms = ["Tshirt", " tshirt "] })) await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        SynonymDto changed;
        using (var update = await PutAsync(created.Id, value with { IsActive = false, RowVersion = created.RowVersion })) { Assert.Equal(HttpStatusCode.OK, update.StatusCode); changed = (await update.Content.ReadFromJsonAsync<SynonymDto>())!; }
        using (var stale = await PutAsync(created.Id, value with { RowVersion = created.RowVersion })) await ProblemAsync(stale, HttpStatusCode.Conflict);
        using (var staleDelete = await client.DeleteBrowserAsync(Route + "/" + created.Id, new SynonymDelete(created.RowVersion))) await ProblemAsync(staleDelete, HttpStatusCode.Conflict);
        var page = await client.GetFromJsonAsync<SynonymPage>(Route + "?pageSize=100"); var after = Assert.Single(page!.Items, item => item.Id == created.Id); Assert.False(after.IsActive); Assert.Equal(value.Terms.Order().ToArray(), after.Terms.Order().ToArray());
        using (var invalidPage = await client.GetAsync(Route + "?pageSize=101")) await ProblemAsync(invalidPage, HttpStatusCode.BadRequest);
        using (var removed = await client.DeleteBrowserAsync(Route + "/" + created.Id, new SynonymDelete(changed.RowVersion))) Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using var publicRead = await client.GetAsync("/api/v1/search/synonym-groups"); Assert.Equal(HttpStatusCode.NotFound, publicRead.StatusCode);
    }
    [Fact]
    public async Task Mutations_use_existing_https_csrf_and_shared_quota()
    {
        await LoginAsync(); var value = new SynonymWrite("اسم", "Name", true, ["term"]);
        using (var missing = await client.PostAsJsonAsync(Route, value)) await ProblemAsync(missing, HttpStatusCode.BadRequest);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost" + Route) { Content = JsonContent.Create(value) })
        { await BrowserRequests.AddCsrfAsync(client, request); using var response = await client.SendAsync(request); Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }); }
        var limited = false;
        for (var i = 0; i < 61; i++)
        {
            using var response = await client.PostAsJsonAsync(i % 2 == 0 ? Route : "/api/v1/admin/locations/governorates", value);
            if (response.StatusCode == HttpStatusCode.TooManyRequests) { await ProblemAsync(response, HttpStatusCode.TooManyRequests); Assert.NotNull(response.Headers.RetryAfter); limited = true; break; }
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.True(limited);
    }
}
