using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Identity;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public sealed class CatalogPersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    [Fact]
    public async Task Migration_supports_bilingual_catalog_and_opaque_versions()
    {
        await using var context = database.CreateContext();
        var activity = Activity.Create("أثاث " + Guid.NewGuid(), "Furniture " + Guid.NewGuid());
        var root = Category.Create(activity.Id, null, "منزلي", "Home");
        var leaf = Category.Create(activity.Id, root.Id, "طاولات", "Tables");
        context.AddRange(activity, root, leaf); await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var saved = await context.Set<Category>().SingleAsync(value => value.Id == leaf.Id);
        Assert.Equal(activity.Id, saved.ActivityId); Assert.Equal(root.Id, saved.ParentId);
        Assert.Equal("طاولات", saved.NameAr); Assert.Equal("Tables", saved.NameEn);
        Assert.Equal(8, saved.RowVersion.Length);
        Assert.Contains((await context.Database.GetAppliedMigrationsAsync()), value => value.Contains("P03CatalogFoundation"));
    }

    [Fact]
    public async Task Catalog_migration_preserves_a_P02_user_and_credentials()
    {
        await using var legacy = new SqlDatabase { InitialMigration = "20261111082824_P02AccountDeletionAndIdentifierKeyBinding" };
        await legacy.InitializeAsync();
        await using var context = legacy.CreateContext();
        var id = Guid.CreateVersion7(); var name = "existing" + Guid.NewGuid().ToString("N");
        var email = name + "@example.invalid"; var hash = "test-only-preserved-hash";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT [identity].[Users] ([Id],[AccountType],[UserName],[NormalizedUserName],[Email],[NormalizedEmail],
                [PasswordHash],[EmailConfirmed],[PhoneNumberConfirmed],[TwoFactorEnabled],[LockoutEnabled],[AccessFailedCount])
            VALUES ({id},1,{name},{name.ToUpperInvariant()},{email},{email.ToUpperInvariant()},{hash},0,0,0,1,0)
            """);
        await context.Database.MigrateAsync();
        var after = await context.Set<ApplicationUser>().AsNoTracking().SingleAsync(value => value.Id == id);
        Assert.Equal(email, after.Email); Assert.Equal(hash, after.PasswordHash);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Activity_names_are_unique_in_each_language(bool arabic)
    {
        await using var context = database.CreateContext();
        var suffix = Guid.NewGuid().ToString("N");
        var first = Activity.Create("عربي " + suffix, "English " + suffix);
        context.Add(first); await context.SaveChangesAsync();
        context.Add(Activity.Create(arabic ? first.NameAr : "مختلف " + suffix, arabic ? "Other " + suffix : first.NameEn.ToLowerInvariant()));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Duplicate_names_are_rejected_for_root_and_child_siblings(bool children, bool arabic)
    {
        await using var context = database.CreateContext();
        var activity = Activity.Create("نشاط " + Guid.NewGuid(), "Activity " + Guid.NewGuid());
        var root = Category.Create(activity.Id, null, "جذر", "Root");
        context.AddRange(activity, root); await context.SaveChangesAsync();
        var parentId = children ? root.Id : (Guid?)null;
        context.Add(Category.Create(activity.Id, parentId, "أول", "First")); await context.SaveChangesAsync();
        context.Add(Category.Create(activity.Id, parentId, arabic ? "أول" : "آخر", arabic ? "Different" : " Ｆirst "));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.Set<Category>().CountAsync(value => value.ActivityId == activity.Id && value.NameEnKey == "FIRST"));
    }

    [Fact]
    public async Task Parent_must_exist_and_belong_to_the_same_activity_and_delete_is_restricted()
    {
        await using var context = database.CreateContext();
        var first = Activity.Create("نشاط " + Guid.NewGuid(), "Activity " + Guid.NewGuid());
        var second = Activity.Create("نشاط " + Guid.NewGuid(), "Activity " + Guid.NewGuid());
        var root = Category.Create(first.Id, null, "جذر", "Root");
        context.AddRange(first, second, root); await context.SaveChangesAsync();
        context.Add(Category.Create(second.Id, root.Id, "خاطئ", "Wrong"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        context.Add(Category.Create(first.Id, Guid.CreateVersion7(), "يتيم", "Orphan"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        var child = Category.Create(first.Id, root.Id, "ابن", "Child"); context.Add(child); await context.SaveChangesAsync();
        context.Remove(await context.Set<Category>().SingleAsync(value => value.Id == root.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        Assert.True(await context.Set<Category>().AnyAsync(value => value.Id == child.Id));
        context.Remove(await context.Set<Activity>().SingleAsync(value => value.Id == first.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Sql_self_parent_constraint_rejects_even_a_direct_update()
    {
        await using var context = database.CreateContext();
        var activity = Activity.Create("نشاط " + Guid.NewGuid(), "Activity " + Guid.NewGuid());
        var root = Category.Create(activity.Id, null, "جذر", "Root");
        context.AddRange(activity, root); await context.SaveChangesAsync();
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [catalog].[Categories] SET [ParentId] = [Id] WHERE [Id] = {root.Id}"));
        context.ChangeTracker.Clear();
        Assert.Null((await context.Set<Category>().SingleAsync(value => value.Id == root.Id)).ParentId);
    }

    [Fact]
    public async Task Same_names_in_other_branches_are_valid_and_a_failed_transaction_rolls_back()
    {
        await using var context = database.CreateContext();
        var activity = Activity.Create("نشاط " + Guid.NewGuid(), "Activity " + Guid.NewGuid());
        context.Add(activity); await context.SaveChangesAsync();
        var first = Category.Create(activity.Id, null, "أول", "First"); var second = Category.Create(activity.Id, null, "ثاني", "Second");
        context.AddRange(first, second); await context.SaveChangesAsync();
        context.AddRange(Category.Create(activity.Id, first.Id, "مشترك", "Shared"), Category.Create(activity.Id, second.Id, "مشترك", "Shared"));
        await context.SaveChangesAsync();
        var rolledBackId = Guid.CreateVersion7();
        await using (var transaction = await context.BeginAsync())
        {
            context.Add(Category.Create(activity.Id, null, "تراجع", "Rollback", id: rolledBackId)); await context.SaveChangesAsync();
        }
        context.ChangeTracker.Clear();
        Assert.False(await context.Set<Category>().AnyAsync(value => value.Id == rolledBackId));
    }
}
