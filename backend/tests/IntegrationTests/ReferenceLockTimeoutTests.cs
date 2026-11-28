using System.Data.Common;
using HawalekNow.Infrastructure.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HawalekNow.IntegrationTests;

public sealed class ReferenceLockTimeoutTests(SqlDatabase database) : IClassFixture<SqlDatabase>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_seed_lock_has_enough_command_time_and_restores_previous_setting(bool locations)
    {
        var capture = new Capture();
        await using var context = new HawalekDbContext(new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(database.ConnectionString, sql => sql.UseNetTopologySuite()).AddInterceptors(capture).Options);
        context.Database.SetCommandTimeout(7);
        await using (var transaction = await context.BeginAsync())
        {
            if (locations) await new LocationStateLock(context).AcquireAsync(false, default, 35000); else await new CatalogTreeLock(context).AcquireAsync(default, 35000);
            Assert.Equal(40, capture.Seconds); Assert.Equal(7, context.Database.GetCommandTimeout());
            await transaction.CommitAsync();
        }
        context.Database.SetCommandTimeout(null); capture.Seconds = null;
        await using (var transaction = await context.BeginAsync())
        {
            if (locations) await new LocationStateLock(context).AcquireAsync(false, default); else await new CatalogTreeLock(context).AcquireAsync(default);
            Assert.Equal(30, capture.Seconds); Assert.Null(context.Database.GetCommandTimeout());
        }
    }
    private sealed class Capture : DbCommandInterceptor
    {
        public int? Seconds { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { if (command.CommandText.Contains("sp_getapplock", StringComparison.Ordinal)) Seconds = command.CommandTimeout; return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken); }
    }
}
