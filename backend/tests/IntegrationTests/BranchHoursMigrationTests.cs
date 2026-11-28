using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Merchants;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public sealed class BranchHoursMigrationTests
{
    [Fact]
    public async Task An_existing_S04_place_gets_an_explicit_closed_week_without_a_permanent_default()
    {
        var database = new SqlDatabase { InitialMigration = "20261126152640_P04BranchesAndAdministrativeReferences" };
        try
        {
            await database.InitializeAsync(); await using var factory = new IdentityApiFactory(database); _ = factory.Services;
            var owner = await MerchantTestAccounts.CreateAsync(factory, AccountType.Merchant);
            // Use the historical Store shape: later moderation columns do not exist at this migration yet.
            var storeId = Guid.NewGuid(); var activity = await StoreTestData.ActivityAsync(database); var path = await BranchTestData.PathAsync(database); var id = Guid.NewGuid();
            await using var context = database.CreateContext();
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [merchants].[Stores] ([Id],[MerchantAccountId],[ActivityId],[Name],[Phone]) VALUES ({storeId},{owner.ProfileId},{activity},N'Existing store','+201012345678')");
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO [merchants].[Branches] ([Id],[StoreId],[Kind],[Name],[Address],[GovernorateId],[CityId],[AreaId],[Position],[PublicFixedAddressConfirmed]) VALUES ({id},{storeId},1,N'Existing',N'Public address',{path.Governorate},{path.City},{path.Area},geography::Point(30,31,4326),1)");
            await context.Database.MigrateAsync(); var branch = await context.Set<MerchantBranch>().SingleAsync(value => value.Id == id);
            Assert.Equal(WeeklyHoursJson.Write(WeeklyHours.AllClosed), WeeklyHoursJson.Write(branch.Hours)); Assert.Equal("Existing", branch.Name);
            var defaults = await context.Database.SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.default_constraints d JOIN sys.columns c ON c.object_id=d.parent_object_id AND c.column_id=d.parent_column_id WHERE d.parent_object_id=OBJECT_ID('merchants.Branches') AND c.name='Hours'").SingleAsync(); Assert.Equal(0, defaults);
        }
        finally { await database.DisposeAsync(); }
    }
}
