using System.Net;
using System.Net.Http.Json;
using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HawalekNow.IntegrationTests;

public sealed class BranchHoursTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly ManualTimeProvider clock = new(TimeProvider.System.GetUtcNow());
    private IdentityApiFactory factory = null!;
    public Task InitializeAsync() { factory = new(database, clock); _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private static WorkingDayWrite[] AlwaysOpen() => Enumerable.Range(0, 7).Select(day => new WorkingDayWrite(day, false, [new("00:00", "00:00", true)])).ToArray();
    private async Task<(MerchantTestAccount Owner, StoreDto Store, BranchWrite Write)> SetupAsync()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        var store = await StoreTestData.CreateAsync(database, owner, new LocalFileStorage(StoreTestData.Root(), clock));
        return (owner, store, BranchTestData.Write(await BranchTestData.PathAsync(database)));
    }
    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string route, object value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, route) { Content = JsonContent.Create(value) }; await BrowserRequests.AddCsrfAsync(client, request); return await client.SendAsync(request);
    }
    [Theory]
    [InlineData("Branch")]
    [InlineData("PickupPoint")]
    public async Task HTTP_requires_a_complete_week_and_returns_hours_and_status_without_hiding_closed_places(string kind)
    {
        var setup = await SetupAsync(); var admin = await MerchantTestAccounts.CreateAsync(factory, AccountType.Admin);
        using var client = factory.CreateHttpsClient(); using var administrator = factory.CreateHttpsClient(); using var guest = factory.CreateHttpsClient();
        await MerchantTestAccounts.LoginAsync(client, setup.Owner); await MerchantTestAccounts.LoginAsync(administrator, admin);
        var route = $"/api/v1/merchants/stores/{setup.Store.Id}/branches"; var write = setup.Write with { Kind = kind };
        foreach (var invalid in new[] { write with { Hours = null }, write with { Hours = [] }, write with { Hours = AlwaysOpen().Skip(1).ToArray() } })
        {
            using var rejected = await client.PostBrowserJsonAsync(route, invalid); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
        using var created = await client.PostBrowserJsonAsync(route, write); Assert.Equal(HttpStatusCode.OK, created.StatusCode); var branch = (await created.Content.ReadFromJsonAsync<BranchDto>())!;
        Assert.Equal(7, branch.Hours.Length); Assert.False(branch.Opening.OpenNow); Assert.Null(branch.Opening.NextOpen);
        using (var approved = await administrator.PostBrowserJsonAsync($"/api/v1/admin/merchants/{setup.Owner.ProfileId}/approve", new { rowVersion = await MerchantTestAccounts.VersionAsync(database, setup.Owner.ProfileId) })) Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using (var visible = await guest.GetAsync($"/api/v1/branches/{branch.Id}"))
        {
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode); var place = (await visible.Content.ReadFromJsonAsync<PublicBranchDto>())!; Assert.False(place.Opening.OpenNow); Assert.Equal(7, place.Hours.Length);
        }
        using (var edited = await PutAsync(client, route + "/" + branch.Id, new BranchUpdateWrite(branch.RowVersion, write with { Hours = AlwaysOpen() })))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode); branch = (await edited.Content.ReadFromJsonAsync<BranchDto>())!; Assert.True(branch.Opening.OpenNow); Assert.Null(branch.Opening.ClosesAt);
        }
        using (var invalidEdit = await PutAsync(client, route + "/" + branch.Id, new BranchUpdateWrite(branch.RowVersion, write with { Hours = null }))) Assert.Equal(HttpStatusCode.BadRequest, invalidEdit.StatusCode);
        using (var publicOpen = await guest.GetAsync($"/api/v1/branches/{branch.Id}")) Assert.True((await publicOpen.Content.ReadFromJsonAsync<PublicBranchDto>())!.Opening.OpenNow);
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); var tomorrow = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).Date.AddDays(1);
        var scheduled = Enumerable.Range(0, 7).Select(day => new WorkingDayWrite(day, day != (int)tomorrow.DayOfWeek, day == (int)tomorrow.DayOfWeek ? [new("09:00", "17:00")] : [])).ToArray();
        using (var edited = await PutAsync(client, route + "/" + branch.Id, new BranchUpdateWrite(branch.RowVersion, write with { Hours = scheduled })))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode); branch = (await edited.Content.ReadFromJsonAsync<BranchDto>())!;
            Assert.False(branch.Opening.OpenNow); Assert.Equal(new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(tomorrow.AddHours(9), zone)), branch.Opening.NextOpen);
        }
        using (var list = await guest.GetAsync($"/api/v1/stores/{setup.Store.Id}/branches")) Assert.Contains((await list.Content.ReadFromJsonAsync<MerchantPage<PublicBranchDto>>())!.Items, value => value.Id == branch.Id && !value.Opening.OpenNow);
    }
    [Fact]
    public async Task SQL_roundtrip_is_required_and_versioned_and_conflicting_hours_commit_once_with_audit()
    {
        var setup = await SetupAsync(); BranchDto branch;
        await using (var context = database.CreateContext()) branch = (await BranchTestData.Service(context).CreateAsync(setup.Owner.Id, setup.Store.Id, setup.Write, default)).Value;
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var results = await Task.WhenAll(BranchTestData.Service(first).UpdateAsync(setup.Owner.Id, setup.Store.Id, branch.Id, new(branch.RowVersion, setup.Write with { Hours = AlwaysOpen() }), default),
            BranchTestData.Service(second).UpdateAsync(setup.Owner.Id, setup.Store.Id, branch.Id, new(branch.RowVersion, setup.Write), default));
        Assert.Single(results, value => value.IsSuccess); Assert.Single(results, value => value.Error?.Kind == ErrorKind.Conflict);
        await using var read = database.CreateContext(); var stored = await read.Set<MerchantBranch>().SingleAsync(value => value.Id == branch.Id);
        Assert.Equal(results.Single(value => value.IsSuccess).Value.Opening.OpenNow, stored.Hours.At(clock.GetUtcNow()).OpenNow);
        Assert.Equal(7, stored.Hours.Days.Length); Assert.False(read.Database.HasPendingModelChanges());
        Assert.Single(await read.Set<AuditEntry>().Where(value => value.TargetId == branch.Id && value.Operation == "Branch.Edit").ToArrayAsync());
        var nullHours = await Assert.ThrowsAsync<SqlException>(() => read.Database.ExecuteSqlInterpolatedAsync($"UPDATE [merchants].[Branches] SET [Hours]=NULL WHERE [Id]={branch.Id}")); Assert.Equal(515, nullHours.Number);
        var invalidJson = await Assert.ThrowsAsync<SqlException>(() => read.Database.ExecuteSqlInterpolatedAsync($"UPDATE [merchants].[Branches] SET [Hours]='invalid' WHERE [Id]={branch.Id}")); Assert.Equal(547, invalidJson.Number);
    }
    [Fact]
    public async Task A_failure_after_SQL_save_rolls_back_changed_hours_and_their_audit()
    {
        var setup = await SetupAsync(); BranchDto branch;
        await using (var context = database.CreateContext()) branch = (await BranchTestData.Service(context).CreateAsync(setup.Owner.Id, setup.Store.Id, setup.Write, default)).Value;
        var interceptor = new FailAfterHoursSaved(branch.Id);
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).AddInterceptors(interceptor).Options;
        await using (var writer = new HawalekDbContext(options)) await Assert.ThrowsAsync<IOException>(() => BranchTestData.Service(writer).UpdateAsync(setup.Owner.Id, setup.Store.Id, branch.Id, new(branch.RowVersion, setup.Write with { Hours = AlwaysOpen() }), default));
        Assert.True(interceptor.SawBothInsideTransaction);
        await using var read = database.CreateContext(); Assert.False((await read.Set<MerchantBranch>().SingleAsync(value => value.Id == branch.Id)).Hours.At(clock.GetUtcNow()).OpenNow);
        Assert.False(await read.Set<AuditEntry>().AnyAsync(value => value.TargetId == branch.Id && value.Operation == "Branch.Edit"));
    }
    private sealed class FailAfterHoursSaved(Guid id) : SaveChangesInterceptor
    {
        public bool SawBothInsideTransaction { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is HawalekDbContext context)
            {
                var stored = await context.Set<MerchantBranch>().AsNoTracking().SingleAsync(value => value.Id == id, cancellationToken);
                SawBothInsideTransaction = context.Database.CurrentTransaction is not null && stored.Hours.At(DateTimeOffset.UtcNow).OpenNow && await context.Set<AuditEntry>().AnyAsync(value => value.TargetId == id && value.Operation == "Branch.Edit", cancellationToken);
                throw new IOException("test-only hours failure after SQL Save");
            }
            return result;
        }
    }
}
