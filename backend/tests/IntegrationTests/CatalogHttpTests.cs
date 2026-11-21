using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class CatalogHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    private HttpClient client = null!;
    public Task InitializeAsync() { client = factory.CreateHttpsClient(); return Task.CompletedTask; }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task LoginAsync(AccountType type = AccountType.Admin)
    {
        using var scope = factory.Services.CreateScope();
        var name = "reference" + Guid.NewGuid().ToString("N"); var password = "Test-only-" + Guid.NewGuid().ToString("N");
        var user = ApplicationUser.Create(type, name, name + "@example.invalid"); user.EmailConfirmed = true;
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded);
        using var response = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = type.ToString(), identifier = user.Email, password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private async Task<ActivityAdminDto> ActivityAsync(bool active = true)
    {
        using var response = await client.PostBrowserJsonAsync("/api/v1/admin/catalog/activities", new ActivityWrite("نشاط " + Guid.NewGuid(), "Activity " + Guid.NewGuid(), active));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ActivityAdminDto>())!;
    }
    private async Task<CategoryAdminDto> CategoryAsync(Guid activity, Guid? parent = null, bool active = true)
    {
        using var response = await client.PostBrowserJsonAsync("/api/v1/admin/catalog/categories", new CategoryWrite(activity, parent, "تصنيف " + Guid.NewGuid(), "Category " + Guid.NewGuid(), active));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); return (await response.Content.ReadFromJsonAsync<CategoryAdminDto>())!;
    }
    private async Task<HttpResponseMessage> PutAsync(string path, object value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(value) };
        await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { "code", "correlationId", "status", "title", "type" }, body.RootElement.EnumerateObject().Select(value => value.Name).OrderBy(value => value).ToArray());
    }

    [Fact]
    public async Task OpenApi_describes_actual_catalog_operations_versions_and_security_errors()
    {
        using var response = await client.GetAsync("/openapi/v1.json"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); var paths = document.RootElement.GetProperty("paths");
        foreach (var resource in new[] { "activities", "categories" })
        {
            Assert.Equal(new[] { "get" }, paths.GetProperty("/api/v1/catalog/" + resource).EnumerateObject().Select(value => value.Name).Order().ToArray());
            foreach (var (suffix, methods) in new[] { ("", new[] { "get", "post" }), ("/{id}", new[] { "delete", "put" }) })
            {
                var path = paths.GetProperty("/api/v1/admin/catalog/" + resource + suffix);
                Assert.Equal(methods, path.EnumerateObject().Select(value => value.Name).Order().ToArray());
                foreach (var method in methods)
                {
                    var operation = path.GetProperty(method); var responses = operation.GetProperty("responses");
                    foreach (var code in new[] { "400", "401", "403" }) Assert.True(responses.TryGetProperty(code, out _));
                    if (method != "get")
                    {
                        foreach (var code in new[] { "409", "429" }) Assert.True(responses.TryGetProperty(code, out _));
                        var csrf = Assert.Single(operation.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "X-CSRF-TOKEN");
                        Assert.True(csrf.GetProperty("required").GetBoolean());
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(false, HttpStatusCode.Unauthorized)]
    [InlineData(true, HttpStatusCode.Forbidden)]
    public async Task Admin_operations_are_closed_to_guests_and_regular_users(bool login, HttpStatusCode expected)
    {
        if (login) await LoginAsync(AccountType.Customer);
        var id = Guid.NewGuid();
        foreach (var path in new[] { "/api/v1/admin/catalog/activities", "/api/v1/admin/catalog/categories?activityId=" + id })
        { using var response = await client.GetAsync(path); await ProblemAsync(response, expected); }
        foreach (var path in new[] { "/api/v1/admin/catalog/activities", "/api/v1/admin/catalog/categories" })
        {
            using var create = await client.PostBrowserJsonAsync(path, new { nameAr = "اسم", nameEn = "Name", activityId = id, isActive = true }); await ProblemAsync(create, expected);
            using var update = await PutAsync(path + "/" + id, new { nameAr = "اسم", nameEn = "Name", activityId = id, isActive = true, rowVersion = "AAAAAAAAAAA=" }); await ProblemAsync(update, expected);
            using var delete = await client.DeleteBrowserAsync(path + "/" + id, new DeleteReference("AAAAAAAAAAA=")); await ProblemAsync(delete, expected);
        }
    }

    [Fact]
    public async Task Admin_crud_rejects_self_cycles_wrong_activity_stale_versions_and_referenced_delete()
    {
        await LoginAsync(); var activity = await ActivityAsync(); var other = await ActivityAsync();
        var root = await CategoryAsync(activity.Id); var child = await CategoryAsync(activity.Id, root.Id);
        using (var self = await PutAsync("/api/v1/admin/catalog/categories/" + root.Id, new CategoryWrite(activity.Id, root.Id, root.NameAr, root.NameEn, true, root.RowVersion))) await ProblemAsync(self, HttpStatusCode.Conflict);
        using (var cycle = await PutAsync("/api/v1/admin/catalog/categories/" + root.Id, new CategoryWrite(activity.Id, child.Id, root.NameAr, root.NameEn, true, root.RowVersion))) await ProblemAsync(cycle, HttpStatusCode.Conflict);
        using (var wrong = await client.PostBrowserJsonAsync("/api/v1/admin/catalog/categories", new CategoryWrite(other.Id, root.Id, "خاطئ", "Wrong", true))) await ProblemAsync(wrong, HttpStatusCode.Conflict);
        using (var deletion = await client.DeleteBrowserAsync("/api/v1/admin/catalog/categories/" + root.Id, new DeleteReference(root.RowVersion))) await ProblemAsync(deletion, HttpStatusCode.Conflict);
        using (var deletion = await client.DeleteBrowserAsync("/api/v1/admin/catalog/activities/" + activity.Id, new DeleteReference(activity.RowVersion))) await ProblemAsync(deletion, HttpStatusCode.Conflict);
        CategoryAdminDto renamed;
        using (var update = await PutAsync("/api/v1/admin/catalog/categories/" + child.Id, new CategoryWrite(activity.Id, root.Id, "اسم جديد", "Renamed", true, child.RowVersion)))
        { Assert.Equal(HttpStatusCode.OK, update.StatusCode); renamed = (await update.Content.ReadFromJsonAsync<CategoryAdminDto>())!; }
        using (var stale = await PutAsync("/api/v1/admin/catalog/categories/" + child.Id, new CategoryWrite(activity.Id, null, "ضائع", "Lost", false, child.RowVersion))) await ProblemAsync(stale, HttpStatusCode.Conflict);
        using (var deleted = await client.DeleteBrowserAsync("/api/v1/admin/catalog/categories/" + child.Id, new DeleteReference(renamed.RowVersion))) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var deleted = await client.DeleteBrowserAsync("/api/v1/admin/catalog/categories/" + root.Id, new DeleteReference(root.RowVersion))) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using (var deleted = await client.DeleteBrowserAsync("/api/v1/admin/catalog/activities/" + activity.Id, new DeleteReference(activity.RowVersion))) Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Public_reads_hide_disabled_ancestors_and_internal_fields_and_are_paginated()
    {
        await LoginAsync(); var activity = await ActivityAsync(); var root = await CategoryAsync(activity.Id); var child = await CategoryAsync(activity.Id, root.Id);
        _ = await CategoryAsync(activity.Id, root.Id, false);
        using var visitor = factory.CreateHttpsClient();
        var before = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&parentId={root.Id}&language=en&pageSize=1");
        Assert.Single(before!.Items); Assert.Equal(child.Id, before.Items[0].Id);
        using (var disabled = await PutAsync("/api/v1/admin/catalog/categories/" + root.Id, new CategoryWrite(activity.Id, null, root.NameAr, root.NameEn, false, root.RowVersion)))
        {
            Assert.Equal(HttpStatusCode.OK, disabled.StatusCode); root = (await disabled.Content.ReadFromJsonAsync<CategoryAdminDto>())!;
        }
        var hidden = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&parentId={root.Id}"); Assert.Empty(hidden!.Items);
        using (var enabled = await PutAsync("/api/v1/admin/catalog/categories/" + root.Id, new CategoryWrite(activity.Id, null, root.NameAr, root.NameEn, true, root.RowVersion))) Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        using var response = await visitor.GetAsync($"/api/v1/catalog/categories?activityId={activity.Id}&parentId={root.Id}&language=en");
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("rowVersion", body); Assert.DoesNotContain("isActive", body); Assert.DoesNotContain("nameAr", body);
        var restored = JsonSerializer.Deserialize<CatalogPage<CatalogOption>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)); Assert.Single(restored!.Items);
        using var invalidPage = await visitor.GetAsync("/api/v1/catalog/activities?pageSize=101"); await ProblemAsync(invalidPage, HttpStatusCode.BadRequest);
        using var invalidLanguage = await visitor.GetAsync("/api/v1/catalog/activities?language=fr"); await ProblemAsync(invalidLanguage, HttpStatusCode.BadRequest);
        using var mutateViaRead = await visitor.PostBrowserJsonAsync("/api/v1/catalog/activities", new ActivityWrite("ممنوع", "Forbidden", true)); Assert.Equal(HttpStatusCode.MethodNotAllowed, mutateViaRead.StatusCode);
    }

    [Fact]
    public async Task Mutations_require_csrf_https_valid_input_and_preserve_safe_contracts()
    {
        await LoginAsync();
        using (var noCsrf = await client.PostAsJsonAsync("/api/v1/admin/catalog/activities", new ActivityWrite("اسم", "Name", true))) await ProblemAsync(noCsrf, HttpStatusCode.BadRequest);
        using (var invalid = await client.PostBrowserJsonAsync("/api/v1/admin/catalog/activities", new ActivityWrite("", "Name", true))) await ProblemAsync(invalid, HttpStatusCode.BadRequest);
        using (var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/api/v1/admin/catalog/activities") { Content = JsonContent.Create(new ActivityWrite("اسم", "Name", true)) })
        {
            await BrowserRequests.AddCsrfAsync(client, request); using var response = await client.SendAsync(request);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        }
        var first = await ActivityAsync();
        using var duplicate = await client.PostBrowserJsonAsync("/api/v1/admin/catalog/activities", new ActivityWrite(first.NameAr, "Different " + Guid.NewGuid(), true)); await ProblemAsync(duplicate, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Actual_reference_quota_is_shared_across_mutation_routes()
    {
        await LoginAsync();
        // Rejected CSRF requests consume the real production quota, without creating fixture rows.
        for (var i = 0; i < 60; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/admin/catalog/activities", new ActivityWrite("اسم", "Name", true));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/v1/admin/catalog/categories", new CategoryWrite(Guid.NewGuid(), null, "اسم", "Name", true));
        await ProblemAsync(limited, HttpStatusCode.TooManyRequests);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    [Fact]
    public async Task Pagination_language_and_activation_of_the_activity_are_enforced()
    {
        await LoginAsync(); var activity = await ActivityAsync();
        var first = await CategoryAsync(activity.Id); var second = await CategoryAsync(activity.Id);
        using var visitor = factory.CreateHttpsClient();
        var page1 = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&language=en&pageSize=1");
        var page2 = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&language=en&pageSize=1&page=2");
        Assert.True(page1!.HasMore); Assert.False(page2!.HasMore); Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
        // SQL uniqueidentifier ordering is stable, but differs from Guid.CompareTo's byte ordering.
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(value => value), new[] { page1.Items[0].Id, page2.Items[0].Id }.OrderBy(value => value));
        var repeated = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&language=en&pageSize=1");
        Assert.Equal(page1.Items[0].Id, repeated!.Items[0].Id);
        var arabic = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&language=ar");
        Assert.Contains(arabic!.Items, value => value.Id == first.Id && value.Name == first.NameAr);
        using var disabled = await PutAsync("/api/v1/admin/catalog/activities/" + activity.Id, new ActivityWrite(activity.NameAr, activity.NameEn, false, activity.RowVersion));
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        var hidden = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}"); Assert.Empty(hidden!.Items);
        using var negativePage = await visitor.GetAsync($"/api/v1/catalog/categories?activityId={activity.Id}&page=0"); await ProblemAsync(negativePage, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Moving_a_parent_changes_descendant_visibility_without_rewriting_activation()
    {
        await LoginAsync(); var activity = await ActivityAsync(); var visibleRoot = await CategoryAsync(activity.Id); var hiddenRoot = await CategoryAsync(activity.Id, active: false);
        var child = await CategoryAsync(activity.Id, visibleRoot.Id); var leaf = await CategoryAsync(activity.Id, child.Id);
        using (var move = await PutAsync("/api/v1/admin/catalog/categories/" + child.Id, new CategoryWrite(activity.Id, hiddenRoot.Id, child.NameAr, child.NameEn, true, child.RowVersion)))
        { Assert.Equal(HttpStatusCode.OK, move.StatusCode); child = (await move.Content.ReadFromJsonAsync<CategoryAdminDto>())!; }
        using var visitor = factory.CreateHttpsClient();
        var hidden = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&parentId={child.Id}"); Assert.Empty(hidden!.Items);
        var admin = await client.GetFromJsonAsync<CatalogPage<CategoryAdminDto>>($"/api/v1/admin/catalog/categories?activityId={activity.Id}&parentId={child.Id}");
        Assert.Single(admin!.Items); Assert.Equal(leaf.Id, admin.Items[0].Id); Assert.True(admin.Items[0].IsActive);
        using (var move = await PutAsync("/api/v1/admin/catalog/categories/" + child.Id, new CategoryWrite(activity.Id, visibleRoot.Id, child.NameAr, child.NameEn, true, child.RowVersion))) Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        var shown = await visitor.GetFromJsonAsync<CatalogPage<CatalogOption>>($"/api/v1/catalog/categories?activityId={activity.Id}&parentId={child.Id}"); Assert.Single(shown!.Items);
        using var missingVersion = await PutAsync("/api/v1/admin/catalog/categories/" + child.Id, new CategoryWrite(activity.Id, null, child.NameAr, child.NameEn, true));
        await ProblemAsync(missingVersion, HttpStatusCode.BadRequest);
    }
}
