using System.Data.Common;
using System.Diagnostics;
using HawalekNow.Application.Modules.Locations;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Shared.Caching;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Activity = HawalekNow.Domain.Modules.Catalog.Activity;

namespace HawalekNow.IntegrationTests;

public sealed class ReferenceSeedTests
{
    private static ServiceProvider Services(SqlDatabase database, DbCommandInterceptor? interceptor = null)
    {
        var services = new ServiceCollection(); services.AddLogging().AddPersistence(database.ConnectionString).AddCatalogReferenceData().AddLocationsReferenceData();
        if (interceptor is not null) services.AddDbContext<HawalekDbContext>(options => options.AddInterceptors(interceptor));
        return services.BuildServiceProvider();
    }
    private static async Task<ReferenceSeedOutcome> SeedAsync(ServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>().SeedAsync();
    }
    [Fact]
    public async Task Actual_pinned_source_imports_every_name_and_parent_and_replays_without_duplicate_rows_or_versions()
    {
        await using var database = new SqlDatabase(); await database.InitializeAsync(); await using var provider = Services(database);
        await using (var before = provider.CreateAsyncScope()) Assert.Empty((await before.ServiceProvider.GetRequiredService<ICatalogQueries>().ActivitiesAsync("en", 1, 30, default)).Value.Items);
        var first = await SeedAsync(provider); Assert.Equal(new ReferenceSeedOutcome(4, 27, 365, 5716, ReferenceSeedRules.AcceptedCommit), first);
        await using (var afterSeed = provider.CreateAsyncScope()) Assert.Equal(4, (await afterSeed.ServiceProvider.GetRequiredService<ICatalogQueries>().ActivitiesAsync("en", 1, 30, default)).Value.Items.Length);
        var source = provider.GetRequiredService<IReferenceSeedSource>().Read();
        await using var context = database.CreateContext();
        var receipts = await context.Set<LocationSeedKey>().AsNoTracking().ToDictionaryAsync(value => value.SourceKey);
        var governorates = await context.Set<Governorate>().AsNoTracking().ToDictionaryAsync(value => value.Id);
        var cities = await context.Set<City>().AsNoTracking().ToDictionaryAsync(value => value.Id);
        var areas = await context.Set<Area>().AsNoTracking().ToDictionaryAsync(value => value.Id);
        Assert.Equal(6108, receipts.Count); Assert.Equal(4, await context.Set<Activity>().CountAsync());
        foreach (var place in source.Governorates)
        { var row = governorates[receipts[place.SourceKey].ReferenceId]; Assert.Equal(place.NameAr, row.NameAr); Assert.Equal(place.NameEn, row.NameEn); }
        foreach (var place in source.Cities)
        { var row = cities[receipts[place.SourceKey].ReferenceId]; Assert.Equal(place.NameAr, row.NameAr); Assert.Equal(place.NameEn, row.NameEn); Assert.Equal(receipts[place.ParentSourceKey!].ReferenceId, row.GovernorateId); }
        foreach (var place in source.Areas)
        { var row = areas[receipts[place.SourceKey].ReferenceId]; Assert.Equal(place.NameAr, row.NameAr); Assert.Equal(place.NameEn, row.NameEn); Assert.Equal(receipts[place.ParentSourceKey!].ReferenceId, row.CityId); Assert.Equal(cities[row.CityId].GovernorateId, row.GovernorateId); }
        var second = await SeedAsync(provider); Assert.Equal(new ReferenceSeedOutcome(0, 0, 0, 0, ReferenceSeedRules.AcceptedCommit), second);
        context.ChangeTracker.Clear();
        Assert.Equal(27, await context.Set<Governorate>().CountAsync()); Assert.Equal(365, await context.Set<City>().CountAsync()); Assert.Equal(5716, await context.Set<Area>().CountAsync());
        var after = await context.Set<Area>().AsNoTracking().ToDictionaryAsync(value => value.Id);
        Assert.All(after, pair => Assert.Equal(areas[pair.Key].RowVersion, pair.Value.RowVersion));
        Assert.Equal(14, cities.Values.Count(value => !areas.Values.Any(area => area.CityId == value.Id)));
        Assert.Equal(18, cities.Values.GroupBy(value => (value.GovernorateId, value.NameEn)).Count(group => group.Count() > 1));
    }

    [Fact]
    public async Task Replay_preserves_admin_renames_deactivation_and_intentional_deletion()
    {
        await using var database = new SqlDatabase(); await database.InitializeAsync(); await using var provider = Services(database); await SeedAsync(provider);
        Guid activityId; Guid deletedActivityId; Guid cityId; Guid areaId;
        await using (var context = database.CreateContext())
        {
            var activity = await context.Set<Activity>().FirstAsync(); activityId = activity.Id; activity.Rename("تعديل إدارة", "Admin edited"); activity.SetActive(false);
            var deletedActivity = await context.Set<Activity>().FirstAsync(value => value.Id != activityId); deletedActivityId = deletedActivity.Id; context.Remove(deletedActivity);
            var city = await context.Set<City>().FirstAsync(); cityId = city.Id; city.Edit(city.GovernorateId, "مدينة معدلة", "Edited city", false);
            var area = await context.Set<Area>().FirstAsync(); areaId = area.Id; context.Remove(area); await context.SaveChangesAsync();
        }
        Assert.Equal(new ReferenceSeedOutcome(0, 0, 0, 0, ReferenceSeedRules.AcceptedCommit), await SeedAsync(provider));
        await using var verify = database.CreateContext(); var afterActivity = await verify.Set<Activity>().SingleAsync(value => value.Id == activityId); var afterCity = await verify.Set<City>().SingleAsync(value => value.Id == cityId);
        Assert.Equal("Admin edited", afterActivity.NameEn); Assert.False(afterActivity.IsActive); Assert.Equal("Edited city", afterCity.NameEn); Assert.False(afterCity.IsActive);
        Assert.False(await verify.Set<Area>().AnyAsync(value => value.Id == areaId)); Assert.True(await verify.Set<LocationSeedKey>().AnyAsync(value => value.ReferenceId == areaId));
        Assert.False(await verify.Set<Activity>().AnyAsync(value => value.Id == deletedActivityId)); Assert.True(await verify.Set<ActivitySeedKey>().AnyAsync(value => value.ActivityId == deletedActivityId));
        Assert.Equal(5715, await verify.Set<Area>().CountAsync());
    }

    [Fact]
    public async Task Failure_after_saved_catalog_and_parents_rolls_back_the_whole_seed_and_retry_succeeds()
    {
        await using var database = new SqlDatabase(); await database.InitializeAsync(); var probe = new FailAreaInsert();
        await using (var provider = Services(database, probe))
        {
            var cache = provider.GetRequiredService<IReferenceOptionsCache>();
            var key = new ReferenceOptionsKey(ReferenceDataFamily.Catalog, "activities", "en", null, null, 1, 30);
            await using (var before = provider.CreateAsyncScope()) Assert.Empty((await before.ServiceProvider.GetRequiredService<ICatalogQueries>().ActivitiesAsync("en", 1, 30, default)).Value.Items);
            await Assert.ThrowsAnyAsync<Exception>(() => SeedAsync(provider)); Assert.True(probe.Fired);
            Assert.Empty((await cache.GetAsync(key, _ => throw new InvalidOperationException("Rolled back seed invalidated cache."), default)).Value.Items);
        }
        await using (var context = database.CreateContext())
        {
            Assert.Empty(await context.Set<Activity>().ToArrayAsync()); Assert.Empty(await context.Set<ActivitySeedKey>().ToArrayAsync());
            Assert.Empty(await context.Set<Governorate>().ToArrayAsync()); Assert.Empty(await context.Set<City>().ToArrayAsync()); Assert.Empty(await context.Set<Area>().ToArrayAsync()); Assert.Empty(await context.Set<LocationSeedKey>().ToArrayAsync());
        }
        await using var retry = Services(database); Assert.Equal(5716, (await SeedAsync(retry)).AreasAdded);
    }

    [Fact]
    public async Task Concurrent_seed_calls_use_the_same_stable_source_keys()
    {
        await using var database = new SqlDatabase(); await database.InitializeAsync(); await using var provider = Services(database);
        var results = await Task.WhenAll(SeedAsync(provider), SeedAsync(provider)); Assert.Equal(4, results.Sum(value => value.ActivitiesAdded)); Assert.Equal(27, results.Sum(value => value.GovernoratesAdded)); Assert.Equal(365, results.Sum(value => value.CitiesAdded)); Assert.Equal(5716, results.Sum(value => value.AreasAdded));
        await using var context = database.CreateContext(); Assert.Equal(4, await context.Set<ActivitySeedKey>().CountAsync()); Assert.Equal(6108, await context.Set<LocationSeedKey>().CountAsync());
    }

    [Fact]
    public async Task Actual_program_seed_command_runs_twice_without_admin_credentials_or_external_downloads()
    {
        await using var database = new SqlDatabase(); await database.InitializeAsync();
        async Task<(int Exit, string Output)> Run()
        {
            var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add(typeof(Program).Assembly.Location); info.ArgumentList.Add("--seed-reference-data"); info.Environment["ConnectionStrings__Application"] = database.ConnectionString;
            foreach (var key in info.Environment.Keys.Where(key => key.StartsWith("Identity__AdminSeed__", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start reference seed test process."); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)); try { await process.WaitForExitAsync(timeout.Token); } finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var output = await stdout + await stderr; Assert.DoesNotContain(database.ConnectionString, output); Assert.DoesNotContain(database.IdentifierKey, output); return (process.ExitCode, output);
        }
        var first = await Run(); Assert.Equal(0, first.Exit); Assert.Contains("activities=4; governorates=27; cities=365; areas=5716", first.Output);
        var second = await Run(); Assert.Equal(0, second.Exit); Assert.Contains("activities=0; governorates=0; cities=0; areas=0", second.Output);
        await using var verify = database.CreateContext(); Assert.Equal(0, await verify.Set<HawalekNow.Infrastructure.Modules.Identity.ApplicationUser>().CountAsync());
    }

    private sealed class FailAreaInsert : DbCommandInterceptor
    {
        public bool Fired { get; private set; }
        private void Check(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO [locations].[Areas]", StringComparison.Ordinal) || command.CommandText.Contains("MERGE [locations].[Areas]", StringComparison.Ordinal)) { Fired = true; throw new InvalidOperationException("Test-only partial seed failure."); }
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Check(command); return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Check(command); return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
