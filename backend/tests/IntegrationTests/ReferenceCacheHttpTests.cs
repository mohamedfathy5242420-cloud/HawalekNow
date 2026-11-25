using System.Collections.Immutable;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using HawalekNow.Application.Shared.Caching;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class ReferenceCacheHttpTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly FailUpdate failure = new();
    private IdentityApiFactory factory = null!;
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        factory = new(database, configure: services => services.AddDbContext<HawalekDbContext>(options => options.AddInterceptors(failure))); client = factory.CreateHttpsClient();
        using var scope = factory.Services.CreateScope(); var name = "cache" + Guid.NewGuid().ToString("N"); var password = "Test-only-" + Guid.NewGuid().ToString("N"); var user = ApplicationUser.Create(AccountType.Admin, name, name + "@example.invalid"); user.EmailConfirmed = true;
        Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user, password)).Succeeded);
        using var login = await client.PostBrowserJsonAsync("/api/v1/identity/login", new { accountType = "Admin", identifier = user.Email, password }); Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private sealed record Row(Guid Id, string NameAr, string NameEn, bool IsActive, string RowVersion);
    private sealed record Selection(ImmutableArray<ReferenceOptionSnapshot> Items);
    private async Task<(Row Parent, Row Child, string ParentRoute, string ChildRoute, string PublicRoute, ReferenceOptionsKey Key)> SetupAsync(bool locations)
    {
        var parentRoute = "/api/v1/admin/" + (locations ? "locations/governorates" : "catalog/activities"); var childRoute = "/api/v1/admin/" + (locations ? "locations/cities" : "catalog/categories"); var suffix = Guid.NewGuid().ToString();
        Row parent; using (var response = await client.PostBrowserJsonAsync(parentRoute, new { nameAr = "أب " + suffix, nameEn = "Parent " + suffix, isActive = true })) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); parent = (await response.Content.ReadFromJsonAsync<Row>())!; }
        object body = locations ? new { nameAr = "ابن", nameEn = "Before", isActive = true, governorateId = parent.Id } : new { nameAr = "ابن", nameEn = "Before", isActive = true, activityId = parent.Id };
        Row child; using (var response = await client.PostBrowserJsonAsync(childRoute, body)) { Assert.Equal(HttpStatusCode.OK, response.StatusCode); child = (await response.Content.ReadFromJsonAsync<Row>())!; }
        var route = locations ? $"/api/v1/locations/cities?governorateId={parent.Id}&language=en" : $"/api/v1/catalog/categories?activityId={parent.Id}&language=en";
        return (parent, child, parentRoute, childRoute, route, new(locations ? ReferenceDataFamily.Locations : ReferenceDataFamily.Catalog, locations ? "City" : "categories", "en", parent.Id, null, 1, 30));
    }
    private object Body(bool locations, Row parent, Row child, string name, string version) => locations ?
        new { nameAr = "اسم", nameEn = name, isActive = true, governorateId = parent.Id, rowVersion = version } :
        new { nameAr = "اسم", nameEn = name, isActive = true, activityId = parent.Id, rowVersion = version };
    private async Task<HttpResponseMessage> PutAsync(string route, Guid id, object value)
    { using var request = new HttpRequestMessage(HttpMethod.Put, route + "/" + id) { Content = JsonContent.Create(value) }; await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request); }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Old_real_sql_read_after_admin_commit_cannot_refill_cache_and_disabled_ancestor_hides_cached_children(bool locations)
    {
        var data = await SetupAsync(locations); var cache = factory.Services.GetRequiredService<IReferenceOptionsCache>(); cache.Invalidate(data.Key.Family);
        var captured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = cache.GetAsync(data.Key, async ct =>
        {
            ReferencePageSnapshot snapshot;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                if (locations)
                { var page = (await scope.ServiceProvider.GetRequiredService<LocationQueries>().OptionsAsync(HawalekNow.Application.Modules.Locations.LocationKind.City, data.Parent.Id, null, "en", 1, 30, ct)).Value; snapshot = new(page.Items.Select(item => new ReferenceOptionSnapshot(item.Id, item.Name)).ToImmutableArray(), page.Page, page.PageSize, page.HasMore); }
                else
                { var page = (await scope.ServiceProvider.GetRequiredService<CatalogQueries>().CategoriesAsync(data.Parent.Id, null, "en", 1, 30, ct)).Value; snapshot = new(page.Items.Select(item => new ReferenceOptionSnapshot(item.Id, item.Name)).ToImmutableArray(), page.Page, page.PageSize, page.HasMore); }
            }
            Assert.Equal("Before", Assert.Single(snapshot.Items).Name); captured.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(30), ct); return Result<ReferencePageSnapshot>.Success(snapshot);
        }, default);
        try
        {
            await captured.Task.WaitAsync(TimeSpan.FromSeconds(20));
            using (var update = await PutAsync(data.ChildRoute, data.Child.Id, Body(locations, data.Parent, data.Child, "After", data.Child.RowVersion))) Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            Assert.Equal("After", Assert.Single((await client.GetFromJsonAsync<Selection>(data.PublicRoute))!.Items).Name);
        }
        finally { release.TrySetResult(); }
        Assert.Equal("Before", Assert.Single((await old).Value.Items).Name);
        var after = await cache.GetAsync(data.Key, _ => throw new InvalidOperationException("Stale SQL read replaced fresh HTTP cache."), default); Assert.Equal("After", Assert.Single(after.Value.Items).Name);
        using (var disable = await PutAsync(data.ParentRoute, data.Parent.Id, new { data.Parent.NameAr, data.Parent.NameEn, isActive = false, rowVersion = data.Parent.RowVersion })) Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<Selection>(data.PublicRoute))!.Items);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_sql_rollback_and_stale_http_write_do_not_invalidate_successful_cached_snapshot(bool locations)
    {
        var data = await SetupAsync(locations); var cache = factory.Services.GetRequiredService<IReferenceOptionsCache>();
        Assert.Equal("Before", Assert.Single((await client.GetFromJsonAsync<Selection>(data.PublicRoute))!.Items).Name);
        var before = await cache.GetAsync(data.Key, _ => throw new InvalidOperationException("Public route did not use cache."), default);
        failure.Armed = true;
        using (var rejected = await PutAsync(data.ChildRoute, data.Child.Id, Body(locations, data.Parent, data.Child, "Rolled back", data.Child.RowVersion))) Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.True(failure.Fired); failure.Armed = false;
        Assert.Same(before.Value, (await cache.GetAsync(data.Key, _ => throw new InvalidOperationException("Rollback incorrectly invalidated cache."), default)).Value);
        await using (var context = database.CreateContext())
        {
            if (locations) { var row = await context.Set<City>().SingleAsync(value => value.Id == data.Child.Id); Assert.Equal("Before", row.NameEn); Assert.Equal(data.Child.RowVersion, Convert.ToBase64String(row.RowVersion)); }
            else { var row = await context.Set<Category>().SingleAsync(value => value.Id == data.Child.Id); Assert.Equal("Before", row.NameEn); Assert.Equal(data.Child.RowVersion, Convert.ToBase64String(row.RowVersion)); }
        }
        using (var update = await PutAsync(data.ChildRoute, data.Child.Id, Body(locations, data.Parent, data.Child, "Committed", data.Child.RowVersion))) Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Committed", Assert.Single((await client.GetFromJsonAsync<Selection>(data.PublicRoute))!.Items).Name);
        var committed = await cache.GetAsync(data.Key, _ => throw new InvalidOperationException("New read missed cache."), default);
        using (var stale = await PutAsync(data.ChildRoute, data.Child.Id, Body(locations, data.Parent, data.Child, "Lost", data.Child.RowVersion))) Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Same(committed.Value, (await cache.GetAsync(data.Key, _ => throw new InvalidOperationException("Rejected stale write invalidated cache."), default)).Value);
    }
    private sealed class FailUpdate : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public bool Fired { get; private set; }
        private void Check(DbCommand command)
        { if (Armed && (command.CommandText.Contains("UPDATE [catalog].[Categories]", StringComparison.Ordinal) || command.CommandText.Contains("UPDATE [locations].[Cities]", StringComparison.Ordinal))) { Fired = true; throw new DbUpdateException("Test-only failed write."); } }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Check(command); return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Check(command); return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
