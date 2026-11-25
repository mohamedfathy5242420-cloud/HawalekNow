using System.Data.Common;
using HawalekNow.Application.Modules.Search;
using HawalekNow.Application.Shared.Results;
using HawalekNow.Domain.Modules.Search;
using HawalekNow.Infrastructure.Modules.Search;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HawalekNow.IntegrationTests;

public sealed class SynonymPersistenceTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    [Fact]
    public async Task Actual_unique_index_and_fk_reject_duplicate_membership_and_orphans()
    {
        await using var context = database.CreateContext(); var group = SynonymGroup.Create("اسم", "Name", false); context.Add(group); var key = Guid.NewGuid().ToString(); context.Add(SynonymTerm.Create(group.Id, key)); await context.SaveChangesAsync();
        context.Add(SynonymTerm.Create(group.Id, key.ToUpperInvariant())); await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync()); context.ChangeTracker.Clear();
        context.Add(SynonymTerm.Create(Guid.NewGuid(), Guid.NewGuid().ToString())); await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
    [Fact]
    public async Task Terms_only_edit_advances_version_and_stale_or_cross_group_conflict_preserves_aggregate()
    {
        await using var context = database.CreateContext(); var service = new SynonymAdministration(context); var suffix = Guid.NewGuid().ToString();
        var original = (await service.SaveAsync(null, new("أصل", "Original", true, [suffix]), default)).Value;
        var changed = (await service.SaveAsync(original.Id, new("أصل", "Original", true, [suffix, "جديد" + suffix], original.RowVersion), default)).Value; Assert.NotEqual(original.RowVersion, changed.RowVersion);
        Assert.Equal(ErrorKind.Conflict, (await service.SaveAsync(original.Id, new("ضائع", "Lost", false, [suffix], original.RowVersion), default)).Error?.Kind);
        Assert.Equal(ErrorKind.Conflict, (await service.SaveAsync(null, new("ثان", "Second", false, [suffix.ToUpperInvariant()]), default)).Error?.Kind);
        Assert.Equal(ErrorKind.Conflict, (await service.DeleteAsync(original.Id, original.RowVersion, default)).Error?.Kind);
        await using var verify = database.CreateContext(); var row = await verify.Set<SynonymGroup>().SingleAsync(value => value.Id == original.Id); Assert.True(row.IsActive); Assert.Equal("Original", row.NameEn); Assert.Equal(2, await verify.Set<SynonymTerm>().CountAsync(value => value.GroupId == row.Id));
        Assert.True((await service.DeleteAsync(row.Id, changed.RowVersion, default)).IsSuccess); Assert.Empty(await verify.Set<SynonymTerm>().Where(value => value.GroupId == row.Id).ToArrayAsync());
    }
    [Fact]
    public async Task Concurrent_groups_competing_for_one_normalized_term_have_one_winner()
    {
        await using var gate = database.CreateContext(); await using var transaction = await gate.BeginAsync();
        await gate.Database.ExecuteSqlRawAsync("EXEC sys.sp_getapplock @Resource=N'search-synonym-membership', @LockMode=N'Exclusive', @LockOwner=N'Transaction'");
        await using var first = database.CreateContext(); await using var second = database.CreateContext(); var key = Guid.NewGuid().ToString();
        var one = new SynonymAdministration(first).SaveAsync(null, new("أول", "First", true, [key]), default); var two = new SynonymAdministration(second).SaveAsync(null, new("ثاني", "Second", false, [" " + key.ToUpperInvariant() + " "]), default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (await gate.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.dm_tran_locks WHERE resource_type='APPLICATION' AND resource_database_id=DB_ID() AND request_status='WAIT'").SingleAsync(timeout.Token) < 2) await Task.Delay(25, timeout.Token);
        Assert.False(one.IsCompleted); Assert.False(two.IsCompleted); await transaction.CommitAsync(); var outcomes = await Task.WhenAll(one, two); Assert.Single(outcomes, value => value.IsSuccess); Assert.Single(outcomes, value => value.Error?.Kind == ErrorKind.Conflict);
    }
    [Fact]
    public async Task Failure_after_group_and_old_terms_saved_rolls_back_rename_version_and_terms()
    {
        Guid id; string version; var key = Guid.NewGuid().ToString();
        await using (var setup = database.CreateContext()) { var result = (await new SynonymAdministration(setup).SaveAsync(null, new("أصل", "Before", true, [key]), default)).Value; id = result.Id; version = result.RowVersion; }
        var probe = new FailTerms(); var options = new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString).AddInterceptors(probe).Options;
        await using (var context = new HawalekDbContext(options)) Assert.Equal(ErrorKind.Conflict, (await new SynonymAdministration(context).SaveAsync(id, new("تغيير", "After", false, [key, "extra"], version), default)).Error?.Kind);
        Assert.True(probe.Fired); await using var verify = database.CreateContext(); var after = await verify.Set<SynonymGroup>().SingleAsync(value => value.Id == id); Assert.Equal("Before", after.NameEn); Assert.True(after.IsActive); Assert.Equal(version, Convert.ToBase64String(after.RowVersion)); Assert.Equal(key, (await verify.Set<SynonymTerm>().SingleAsync(value => value.GroupId == id)).Original);
    }
    private sealed class FailTerms : DbCommandInterceptor
    {
        public bool Fired { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { if (command.CommandText.Contains("INSERT INTO [search].[SynonymTerms]", StringComparison.Ordinal) || command.CommandText.Contains("MERGE [search].[SynonymTerms]", StringComparison.Ordinal)) { Fired = true; throw new InvalidOperationException("Test-only replacement failure."); } return base.ReaderExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
