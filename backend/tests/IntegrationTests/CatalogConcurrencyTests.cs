using HawalekNow.Infrastructure.Shared.Caching;
using HawalekNow.Application.Modules.Catalog;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Catalog;
using Microsoft.EntityFrameworkCore;

namespace HawalekNow.IntegrationTests;

public sealed class CatalogConcurrencyTests(SqlDatabase database) : IClassFixture<SqlDatabase>, IDisposable
{
    private readonly ReferenceOptionsCache cache = new(TimeProvider.System);
    public void Dispose() => cache.Dispose();

    [Fact]
    public async Task Opposite_moves_that_are_individually_valid_cannot_create_a_cycle()
    {
        await using var setup = database.CreateContext();
        var activity = Activity.Create("تنافس " + Guid.NewGuid(), "Race " + Guid.NewGuid());
        var first = Category.Create(activity.Id, null, "أول", "First"); var second = Category.Create(activity.Id, null, "ثاني", "Second");
        setup.AddRange(activity, first, second); await setup.SaveChangesAsync();
        var firstVersion = Convert.ToBase64String(first.RowVersion); var secondVersion = Convert.ToBase64String(second.RowVersion);
        await using var gate = database.CreateContext(); await using var transaction = await gate.BeginAsync();
        await new CatalogTreeLock(gate).AcquireAsync(default);
        await using var firstContext = database.CreateContext(); await using var secondContext = database.CreateContext();
        var firstMove = new CatalogAdministration(firstContext, new(firstContext), cache).SaveCategoryAsync(first.Id,
            new(activity.Id, second.Id, first.NameAr, first.NameEn, true, firstVersion), default);
        var secondMove = new CatalogAdministration(secondContext, new(secondContext), cache).SaveCategoryAsync(second.Id,
            new(activity.Id, first.Id, second.NameAr, second.NameEn, true, secondVersion), default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var waiters = 0;
        while (waiters < 2)
        {
            waiters = await gate.Database.SqlQueryRaw<int>("""
                SELECT COUNT(*) AS [Value] FROM sys.dm_tran_locks
                WHERE [resource_type] = 'APPLICATION' AND [resource_database_id] = DB_ID()
                AND [request_status] = 'WAIT'
                """).SingleAsync(timeout.Token);
            if (waiters < 2) await Task.Delay(25, timeout.Token);
        }
        Assert.False(firstMove.IsCompleted); Assert.False(secondMove.IsCompleted);
        await transaction.CommitAsync();
        var results = await Task.WhenAll(firstMove, secondMove);
        Assert.Single(results, value => value.IsSuccess);
        Assert.Single(results, value => value.Error?.Kind == ErrorKind.Conflict);
        await using var verify = database.CreateContext();
        var afterFirst = await verify.Set<Category>().SingleAsync(value => value.Id == first.Id);
        var afterSecond = await verify.Set<Category>().SingleAsync(value => value.Id == second.Id);
        Assert.True((afterFirst.ParentId == second.Id && afterSecond.ParentId is null) || (afterSecond.ParentId == first.Id && afterFirst.ParentId is null));
    }

    [Fact]
    public async Task Stale_version_and_duplicate_name_roll_back_without_losing_an_update()
    {
        await using var context = database.CreateContext(); var service = new CatalogAdministration(context, new(context), cache);
        var activity = await service.SaveActivityAsync(null, new("نسخة " + Guid.NewGuid(), "Version " + Guid.NewGuid(), true), default);
        Assert.True(activity.IsSuccess);
        var category = await service.SaveCategoryAsync(null, new(activity.Value.Id, null, "أصل", "Original", true), default);
        Assert.True(category.IsSuccess);
        var changed = await service.SaveCategoryAsync(category.Value.Id, new(activity.Value.Id, null, "جديد", "Changed", true, category.Value.RowVersion), default);
        Assert.True(changed.IsSuccess);
        await using var otherContext = database.CreateContext(); var other = new CatalogAdministration(otherContext, new(otherContext), cache);
        var stale = await other.SaveCategoryAsync(category.Value.Id, new(activity.Value.Id, null, "ضائع", "Lost", false, category.Value.RowVersion), default);
        Assert.Equal(ErrorKind.Conflict, stale.Error?.Kind);
        var sibling = await other.SaveCategoryAsync(null, new(activity.Value.Id, null, "أخ", "Sibling", true), default); Assert.True(sibling.IsSuccess);
        var duplicate = await service.SaveCategoryAsync(changed.Value.Id, new(activity.Value.Id, null, "أخ", "Sibling", false, changed.Value.RowVersion), default);
        Assert.Equal(ErrorKind.Conflict, duplicate.Error?.Kind);
        await using var verify = database.CreateContext(); var after = await verify.Set<Category>().SingleAsync(value => value.Id == category.Value.Id);
        Assert.Equal("Changed", after.NameEn); Assert.True(after.IsActive); Assert.Null(after.ParentId);
        Assert.Equal(changed.Value.RowVersion, Convert.ToBase64String(after.RowVersion));
        Assert.Equal(ErrorKind.Conflict, (await other.DeleteCategoryAsync(after.Id, category.Value.RowVersion, default)).Error?.Kind);
    }
}
