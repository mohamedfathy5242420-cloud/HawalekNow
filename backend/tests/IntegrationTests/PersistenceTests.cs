using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using Microsoft.EntityFrameworkCore;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.IntegrationTests;

public sealed class PersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    [Fact]
    public async Task Registration_migration_preserves_existing_identity_data_and_does_not_invent_prior_consent()
    {
        await using var legacy = new SqlDatabase { InitialMigration = "20261110120808_P02IdentityFoundation" };
        await legacy.InitializeAsync();
        var user = User(AccountType.Customer, "legacy" + Guid.NewGuid().ToString("N"));
        var originalHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "Test-only-" + Guid.NewGuid().ToString("N"));
        await using var context = legacy.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT [identity].[Users] ([Id],[AccountType],[UserName],[NormalizedUserName],[Email],[NormalizedEmail],
                [PasswordHash],[EmailConfirmed],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount])
            VALUES ({user.Id},{(int)user.AccountType},{user.UserName},{user.NormalizedUserName},{user.Email},{user.NormalizedEmail},
                {originalHash},0,0,0,1,0)
            """);
        await context.Database.MigrateAsync();
        var after = await context.Set<ApplicationUser>().AsNoTracking().SingleAsync();
        Assert.Equal(user.Id, after.Id);
        Assert.Equal(user.Email, after.Email);
        Assert.Equal(originalHash, after.PasswordHash);
        Assert.Null(after.TermsAcceptedAt);
        Assert.Equal(AccountAccessStatus.Active, after.AccessStatus);
        var collation = await context.Database.SqlQueryRaw<string>("SELECT [collation_name] AS [Value] FROM sys.columns WHERE [object_id]=OBJECT_ID(N'[identity].[Users]') AND [name]=N'NormalizedEmail'").SingleAsync();
        Assert.Equal("Latin1_General_100_BIN2", collation);
    }

    [Fact]
    public async Task Identity_UserManager_supports_type_scoped_accounts_and_joins_the_unit_of_work()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(TestIdentityConfiguration.Create(database.ConnectionString, database.IdentifierKey));
        services.AddLogging().AddPersistence(database.ConnectionString).AddMerchantAccounts().AddIdentityPersistence();
        await using var provider = services.BuildServiceProvider();
        var name = "store" + Guid.NewGuid().ToString("N");
        var password = "Test-only-" + Guid.NewGuid().ToString("N");
        Guid customerId;
        Guid merchantId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<HawalekDbContext>();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await using var transaction = await context.BeginAsync();
            var customer = ApplicationUser.Create(AccountType.Customer, name, name + "@example.invalid");
            var merchant = ApplicationUser.Create(AccountType.Merchant, name, name + "@example.invalid");
            customerId = customer.Id;
            merchantId = merchant.Id;
            Assert.True((await manager.CreateAsync(customer, password)).Succeeded);
            Assert.True((await manager.CreateAsync(merchant, password)).Succeeded);
            Assert.NotEqual(password, customer.PasswordHash);
            Assert.True(await manager.CheckPasswordAsync(customer, password));
            var duplicate = await manager.CreateAsync(ApplicationUser.Create(AccountType.Customer, name.ToUpperInvariant(), name + "@example.invalid"), password);
            Assert.False(duplicate.Succeeded);
            Assert.All(duplicate.Errors, error => Assert.Equal("InvalidAccount", error.Code));
            await new MerchantAccounts(context).CreatePendingAsync(merchant.Id);
            await context.SaveChangesAsync();
            // UserOnlyStore's internal SaveChanges must not escape the outer transaction.
        }
        await using var verify = database.CreateContext();
        Assert.False(await verify.Set<ApplicationUser>().AnyAsync(user => user.Id == customerId || user.Id == merchantId));
        Assert.False(await verify.Set<MerchantAccount>().AnyAsync(account => account.UserId == merchantId));
    }
    private static ApplicationUser User(AccountType type, string name)
    {
        var user = ApplicationUser.Create(type, name, name + "@example.invalid");
        user.NormalizedUserName = name.ToUpperInvariant();
        user.NormalizedEmail = user.Email!.ToUpperInvariant();
        return user;
    }

    [Fact]
    public async Task Actual_migrations_create_SQL_schemas_and_have_no_pending_model_changes()
    {
        await using var context = database.CreateContext();
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
        Assert.True(context.Database.IsSqlServer());
        Assert.Equal("identity", context.Model.FindEntityType(typeof(ApplicationUser))!.GetSchema());
        Assert.Equal("merchants", context.Model.FindEntityType(typeof(MerchantAccount))!.GetSchema());
    }

    [Fact]
    public async Task SQL_enforces_type_scoped_uniqueness_and_distinct_account_ids()
    {
        var name = "scope" + Guid.NewGuid().ToString("N");
        await using (var context = database.CreateContext())
        {
            var customer = User(AccountType.Customer, name);
            var merchant = User(AccountType.Merchant, name);
            Assert.NotEqual(customer.Id, merchant.Id);
            Assert.Equal(7, customer.Id.Version);
            context.AddRange(customer, merchant);
            await context.SaveChangesAsync();
        }
        await using var duplicate = database.CreateContext();
        duplicate.Add(User(AccountType.Customer, name));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task Unit_of_work_commits_both_modules_and_disposal_rolls_back_even_after_SaveChanges()
    {
        Guid rolledBackId;
        await using (var context = database.CreateContext())
        {
            await using var transaction = await context.BeginAsync();
            var user = User(AccountType.Merchant, "rollback" + Guid.NewGuid().ToString("N"));
            rolledBackId = user.Id;
            context.Add(user);
            await context.SaveChangesAsync();
            await new MerchantAccounts(context).CreatePendingAsync(user.Id);
            await context.SaveChangesAsync();
            // Simulate failure after both SaveChanges calls by disposing without Commit.
        }
        await using var verify = database.CreateContext();
        Assert.False(await verify.Set<ApplicationUser>().AnyAsync(user => user.Id == rolledBackId));
        Assert.False(await verify.Set<MerchantAccount>().AnyAsync(account => account.UserId == rolledBackId));
        var committed = User(AccountType.Merchant, "commit" + Guid.NewGuid().ToString("N"));
        await using (var transaction = await verify.BeginAsync())
        {
            verify.Add(committed);
            await new MerchantAccounts(verify).CreatePendingAsync(committed.Id);
            await verify.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await using var read = database.CreateContext();
        var merchant = await new MerchantAccounts(read).FindByOwnerAsync(committed.Id);
        Assert.NotNull(merchant);
        Assert.True(merchant.CanPrepareAccount);
        Assert.False(merchant.IsApproved);
        Assert.NotEmpty((await read.Set<ApplicationUser>().SingleAsync(user => user.Id == committed.Id)).RowVersion);
    }

    [Fact]
    public async Task SQL_rowversion_rejects_a_stale_writer()
    {
        await using var first = database.CreateContext();
        var user = User(AccountType.Customer, "concurrency" + Guid.NewGuid().ToString("N"));
        first.Add(user);
        await first.SaveChangesAsync();
        await using var second = database.CreateContext();
        var stale = await second.Set<ApplicationUser>().SingleAsync(value => value.Id == user.Id);
        user.EmailConfirmed = true;
        await first.SaveChangesAsync();
        stale.LockoutEnabled = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
