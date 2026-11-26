using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Locations;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Domain.Shared.Auditing;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HawalekNow.IntegrationTests;

public sealed class BranchPersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IAsyncLifetime
{
    private readonly IdentityApiFactory factory = new(database);
    public Task InitializeAsync() { _ = factory.Services; return Task.CompletedTask; }
    public Task DisposeAsync() => factory.DisposeAsync().AsTask();
    private async Task<(MerchantTestAccount Owner, StoreDto Store)> StoreAsync()
    {
        var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        var store = await StoreTestData.CreateAsync(database, owner, new LocalFileStorage(StoreTestData.Root(), TimeProvider.System)); return (owner, store);
    }
    [Fact]
    public async Task Actual_geography_roundtrip_uses_SRID4326_longitude_X_and_latitude_Y()
    {
        var setup = await StoreAsync(); var path = await BranchTestData.PathAsync(database);
        await using var context = database.CreateContext(); var result = await BranchTestData.Service(context).CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(path), default);
        Assert.True(result.IsSuccess); var id = result.Value.Id; context.ChangeTracker.Clear();
        var branch = await context.Set<MerchantBranch>().SingleAsync(value => value.Id == id);
        Assert.Equal(30.0444, branch.Position.Latitude, 8); Assert.Equal(31.2357, branch.Position.Longitude, 8);
        var srid = await context.Database.SqlQuery<int>($"SELECT [Position].STSrid AS [Value] FROM [merchants].[Branches] WHERE [Id]={id}").SingleAsync();
        var latitude = await context.Database.SqlQuery<double>($"SELECT [Position].Lat AS [Value] FROM [merchants].[Branches] WHERE [Id]={id}").SingleAsync();
        var longitude = await context.Database.SqlQuery<double>($"SELECT [Position].Long AS [Value] FROM [merchants].[Branches] WHERE [Id]={id}").SingleAsync();
        Assert.Equal(4326, srid); Assert.Equal(30.0444, latitude, 8); Assert.Equal(31.2357, longitude, 8);
        var wrongShape = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync($"UPDATE [merchants].[Branches] SET [Position]=geography::STGeomFromText('LINESTRING (31 30,32 31)',4326) WHERE [Id]={id}"));
        Assert.Equal(547, wrongShape.Number); Assert.False(context.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task Full_administrative_path_is_checked_by_contract_and_actual_composite_FK()
    {
        var setup = await StoreAsync(); var first = await BranchTestData.PathAsync(database); var second = await BranchTestData.PathAsync(database); var inactive = await BranchTestData.PathAsync(database, false);
        await using var context = database.CreateContext(); var service = BranchTestData.Service(context);
        Assert.Equal(ErrorKind.Validation, (await service.CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(first) with { CityId = second.City }, default)).Error?.Kind);
        Assert.Equal(ErrorKind.Validation, (await service.CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(inactive), default)).Error?.Kind);
        var branch = (await service.CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(first), default)).Value;
        var deleted = await Assert.ThrowsAsync<SqlException>(() => context.Set<Area>().Where(value => value.Id == first.Area).ExecuteDeleteAsync()); Assert.Equal(547, deleted.Number);
        var moved = await Assert.ThrowsAsync<SqlException>(() => context.Set<Area>().Where(value => value.Id == first.Area)
            .ExecuteUpdateAsync(set => set.SetProperty(value => value.CityId, second.City).SetProperty(value => value.GovernorateId, second.Governorate))); Assert.Equal(547, moved.Number);
        Assert.True(BranchInput.Normalize(BranchTestData.Write(first) with { CityId = second.City, GovernorateId = second.Governorate }, out var invalid));
        context.Add(MerchantBranch.Create(setup.Store.Id, invalid!)); var insert = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); Assert.Equal(547, Assert.IsType<SqlException>(insert.InnerException).Number);
        await using var read = database.CreateContext(); Assert.Single(await read.Set<MerchantBranch>().Where(value => value.StoreId == setup.Store.Id).ToArrayAsync());
        Assert.Equal(first.Area, (await read.Set<MerchantBranch>().SingleAsync(value => value.Id == branch.Id)).AreaId);
    }
    [Fact]
    public async Task A_real_reference_delete_race_cannot_commit_an_orphan()
    {
        var setup = await StoreAsync(); var path = await BranchTestData.PathAsync(database);
        await using var writer = database.CreateContext(); await using var deleter = database.CreateContext();
        async Task<bool> Delete()
        {
            try { return await deleter.Set<Area>().Where(value => value.Id == path.Area).ExecuteDeleteAsync() == 1; }
            catch (SqlException error) when (error.Number is 547 or 1205) { return false; }
        }
        var create = BranchTestData.Service(writer).CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(path), default); var deleted = Delete();
        await Task.WhenAll(create, deleted);
        var creation = await create; var deletion = await deleted;
        await using var read = database.CreateContext(); var exists = await read.Set<Area>().AnyAsync(value => value.Id == path.Area); var count = await read.Set<MerchantBranch>().CountAsync(value => value.StoreId == setup.Store.Id);
        if (creation.IsSuccess) { Assert.False(deletion); Assert.True(exists); Assert.Equal(1, count); }
        else { Assert.True(deletion); Assert.False(exists); Assert.Equal(0, count); Assert.Contains(creation.Error!.Kind, new[] { ErrorKind.Validation, ErrorKind.Conflict }); }
    }
    [Fact]
    public async Task A_failure_after_real_save_rolls_back_the_branch_and_its_audit()
    {
        var setup = await StoreAsync(); var path = await BranchTestData.PathAsync(database); var failure = new FailAfterBranchSaved();
        var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).AddInterceptors(failure).Options;
        await using (var context = new HawalekDbContext(options)) await Assert.ThrowsAsync<IOException>(() => BranchTestData.Service(context).CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(path), default));
        Assert.True(failure.SawBothInsideTransaction); await using var read = database.CreateContext();
        Assert.False(await read.Set<MerchantBranch>().AnyAsync(value => value.StoreId == setup.Store.Id)); Assert.False(await read.Set<AuditEntry>().AnyAsync(value => value.TargetId == failure.Id));
    }
    [Fact]
    public async Task Opposing_updates_commit_one_version_and_preserve_ownership()
    {
        var setup = await StoreAsync(); var path = await BranchTestData.PathAsync(database); BranchDto branch;
        await using (var context = database.CreateContext()) branch = (await BranchTestData.Service(context).CreateAsync(setup.Owner.Id, setup.Store.Id, BranchTestData.Write(path), default)).Value;
        await using var first = database.CreateContext(); await using var second = database.CreateContext();
        var results = await Task.WhenAll(BranchTestData.Service(first).UpdateAsync(setup.Owner.Id, setup.Store.Id, branch.Id, new(branch.RowVersion, BranchTestData.Write(path) with { Name = "First" }), default),
            BranchTestData.Service(second).UpdateAsync(setup.Owner.Id, setup.Store.Id, branch.Id, new(branch.RowVersion, BranchTestData.Write(path) with { Name = "Second" }), default));
        Assert.Single(results, value => value.IsSuccess); Assert.Single(results, value => value.Error?.Kind == ErrorKind.Conflict);
        var other = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
        await using var read = database.CreateContext(); var stored = await read.Set<MerchantBranch>().SingleAsync(value => value.Id == branch.Id);
        Assert.Equal(setup.Store.Id, stored.StoreId); Assert.Equal(ErrorKind.NotFound, (await BranchTestData.Service(read).UpdateAsync(other.Id, setup.Store.Id, branch.Id, new(Convert.ToBase64String(stored.RowVersion), BranchTestData.Write(path)), default)).Error?.Kind);
        Assert.Single(await read.Set<AuditEntry>().Where(value => value.TargetId == branch.Id && value.Operation == "Branch.Edit").ToArrayAsync());
    }
    private sealed class FailAfterBranchSaved : SaveChangesInterceptor
    {
        public Guid Id { get; private set; }
        public bool SawBothInsideTransaction { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context is HawalekDbContext context && context.ChangeTracker.Entries<MerchantBranch>().SingleOrDefault()?.Entity is { } branch)
            {
                Id = branch.Id; SawBothInsideTransaction = context.Database.CurrentTransaction is not null && await context.Set<MerchantBranch>().AsNoTracking().AnyAsync(value => value.Id == branch.Id, cancellationToken)
                    && await context.Set<AuditEntry>().AnyAsync(value => value.TargetId == branch.Id && value.Operation == "Branch.Create", cancellationToken);
                throw new IOException("test-only branch save failure");
            }
            return result;
        }
    }
}
