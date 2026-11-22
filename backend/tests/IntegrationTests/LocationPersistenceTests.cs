using HawalekNow.Infrastructure.Shared.Caching;
using HawalekNow.Application.Modules.Locations;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Infrastructure.Modules.Locations;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public sealed class LocationPersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IDisposable
{
    private readonly ReferenceOptionsCache cache = new(TimeProvider.System);
    public void Dispose() => cache.Dispose();

    [Fact]
    public async Task Composite_foreign_keys_reject_mismatched_and_orphan_parents()
    {
        await using var context = database.CreateContext();
        var first = Governorate.Create("أولى", "First"); var second = Governorate.Create("ثانية", "Second"); var city = City.Create(first.Id, "مدينة", "City");
        context.AddRange(first, second, city); await context.SaveChangesAsync();
        var invalid = Area.Create(second.Id, city.Id, "غير صالح", "Invalid"); context.Add(invalid);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        Assert.False(await context.Set<Area>().AnyAsync(value => value.Id == invalid.Id));
        context.Add(City.Create(Guid.NewGuid(), "يتيم", "Orphan")); await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        context.Add(Area.Create(first.Id, Guid.NewGuid(), "يتيم", "Orphan")); await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Tracked_children_are_never_detached_when_their_parent_is_deleted()
    {
        await using var context = database.CreateContext(); var governorate = Governorate.Create("محافظة", "Governorate");
        var city = City.Create(governorate.Id, "مدينة", "City"); var area = Area.Create(governorate.Id, city.Id, "منطقة", "Area");
        context.AddRange(governorate, city, area); await context.SaveChangesAsync();
        context.Remove(city); await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        context.Remove(await context.Set<Governorate>().SingleAsync(value => value.Id == governorate.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        var after = await context.Set<Area>().SingleAsync(value => value.Id == area.Id); Assert.Equal(city.Id, after.CityId); Assert.Equal(governorate.Id, after.GovernorateId);
    }

    [Fact]
    public async Task Additive_locations_migration_preserves_existing_catalog_rows()
    {
        await using var legacy = new SqlDatabase { InitialMigration = "20261121111950_P03CatalogFoundation" }; await legacy.InitializeAsync();
        await using var context = legacy.CreateContext(); var activity = Activity.Create("نشاط", "Activity"); context.Add(activity); await context.SaveChangesAsync();
        var version = activity.RowVersion.ToArray(); await context.Database.MigrateAsync(); context.ChangeTracker.Clear();
        var after = await context.Set<Activity>().SingleAsync(value => value.Id == activity.Id); Assert.Equal("نشاط", after.NameAr); Assert.Equal(version, after.RowVersion);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task Distinct_districts_with_the_same_English_name_are_preserved_in_sql()
    {
        await using var context = database.CreateContext(); var governorate = Governorate.Create("الدقهلية", "Dakahlia");
        var district = City.Create(governorate.Id, "قسم ميت غمر", "Mit Ghamr"); var center = City.Create(governorate.Id, "مركز ميت غمر", "Mit Ghamr");
        context.AddRange(governorate, district, center); await context.SaveChangesAsync(); context.ChangeTracker.Clear();
        Assert.Equal(2, await context.Set<City>().CountAsync(value => value.GovernorateId == governorate.Id && value.NameEn == "Mit Ghamr"));
    }

    [Fact]
    public async Task City_move_requires_no_children_and_stale_area_writes_preserve_the_prior_data()
    {
        await using var context = database.CreateContext(); var service = new LocationAdministration(context, new(context), cache);
        var first = await service.SaveAsync(LocationKind.Governorate, null, new("أولى", "First", true), default);
        var second = await service.SaveAsync(LocationKind.Governorate, null, new("ثانية", "Second", true), default); Assert.True(first.IsSuccess && second.IsSuccess);
        var city = await service.SaveAsync(LocationKind.City, null, new("مدينة", "City", true, first.Value.Id), default); Assert.True(city.IsSuccess);
        var moved = await service.SaveAsync(LocationKind.City, city.Value.Id, new("مدينة", "City", true, second.Value.Id, RowVersion: city.Value.RowVersion), default); Assert.True(moved.IsSuccess);
        Assert.Equal(second.Value.Id, moved.Value.GovernorateId); Assert.NotEqual(city.Value.RowVersion, moved.Value.RowVersion);
        var area = await service.SaveAsync(LocationKind.Area, null, new("منطقة", "Area", true, second.Value.Id, city.Value.Id), default); Assert.True(area.IsSuccess);
        var denied = await service.SaveAsync(LocationKind.City, city.Value.Id, new("مدينة", "City", true, first.Value.Id, RowVersion: moved.Value.RowVersion), default); Assert.Equal(ErrorKind.Conflict, denied.Error?.Kind);
        var changed = await service.SaveAsync(LocationKind.Area, area.Value.Id, new("محدثة", "Updated", false, second.Value.Id, city.Value.Id, area.Value.RowVersion), default); Assert.True(changed.IsSuccess);
        var stale = await service.SaveAsync(LocationKind.Area, area.Value.Id, new("ضائعة", "Lost", true, second.Value.Id, city.Value.Id, area.Value.RowVersion), default); Assert.Equal(ErrorKind.Conflict, stale.Error?.Kind);
        await using var verify = database.CreateContext(); var after = await verify.Set<Area>().SingleAsync(value => value.Id == area.Value.Id);
        Assert.Equal("Updated", after.NameEn); Assert.False(after.IsActive); Assert.Equal(second.Value.Id, after.GovernorateId);
        Assert.Equal(second.Value.Id, (await verify.Set<City>().SingleAsync(value => value.Id == city.Value.Id)).GovernorateId);
    }

    [Fact]
    public async Task A_failed_outer_transaction_leaves_no_partial_hierarchy()
    {
        await using var context = database.CreateContext(); var governorate = Governorate.Create("تراجع", "Rollback"); var city = City.Create(governorate.Id, "تراجع", "Rollback");
        await using (var transaction = await context.BeginAsync()) { context.AddRange(governorate, city); await context.SaveChangesAsync(); }
        context.ChangeTracker.Clear(); Assert.False(await context.Set<Governorate>().AnyAsync(value => value.Id == governorate.Id)); Assert.False(await context.Set<City>().AnyAsync(value => value.Id == city.Id));
    }

    [Fact]
    public async Task Concurrent_area_creation_and_city_transfer_never_break_the_composite_relationship()
    {
        await using var setup = database.CreateContext(); var first = Governorate.Create("أولى", "First"); var second = Governorate.Create("ثانية", "Second"); var city = City.Create(first.Id, "مدينة", "City");
        setup.AddRange(first, second, city); await setup.SaveChangesAsync();
        await using var gate = database.CreateContext(); await using var transaction = await gate.BeginAsync(); await new LocationStateLock(gate).AcquireAsync(false, default);
        await using var moveContext = database.CreateContext(); await using var createContext = database.CreateContext();
        var move = new LocationAdministration(moveContext, new(moveContext), cache).SaveAsync(LocationKind.City, city.Id, new("مدينة", "City", true, second.Id, RowVersion: Convert.ToBase64String(city.RowVersion)), default);
        var create = new LocationAdministration(createContext, new(createContext), cache).SaveAsync(LocationKind.Area, null, new("منطقة", "Area", true, first.Id, city.Id), default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)); var count = 0;
        while (count < 2)
        {
            count = await gate.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.dm_tran_locks WHERE resource_type='APPLICATION' AND resource_database_id=DB_ID() AND request_status='WAIT'").SingleAsync(timeout.Token);
            if (count < 2) await Task.Delay(25, timeout.Token);
        }
        Assert.False(move.IsCompleted); Assert.False(create.IsCompleted); await transaction.CommitAsync();
        var results = await Task.WhenAll(move, create); Assert.Single(results, value => value.IsSuccess); Assert.Single(results, value => !value.IsSuccess);
        await using var verify = database.CreateContext(); var actualCity = await verify.Set<City>().SingleAsync(value => value.Id == city.Id);
        var areas = await verify.Set<Area>().Where(value => value.CityId == city.Id).ToArrayAsync();
        Assert.All(areas, value => Assert.Equal(actualCity.GovernorateId, value.GovernorateId));
    }
}
