using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HawalekNow.Infrastructure.Shared.Persistence;

public sealed class DesignTimeContextFactory : IDesignTimeDbContextFactory<HawalekDbContext>
{
    public HawalekDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Application")
            ?? throw new InvalidOperationException("Configure ConnectionStrings__Application locally before running migration tools.");
        return new(new DbContextOptionsBuilder<HawalekDbContext>().UseSqlServer(connection,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "operations")).Options);
    }
}
