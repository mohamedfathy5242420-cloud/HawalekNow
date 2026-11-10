using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace HawalekNow.IntegrationTests;

// Every fixture owns a newly created database. It never migrates or deletes an existing application database.
public sealed class SqlDatabase : IAsyncLifetime
{
    private readonly string databaseName = "HN_P02_Test_" + Guid.CreateVersion7().ToString("N");
    private string? masterConnection;
    private bool created;
    public string ConnectionString { get; private set; } = "";
    public string IdentifierKey { get; } = TestIdentityConfiguration.NewKey();
    public string? InitialMigration { get; init; }

    public HawalekDbContext CreateContext() => new(new DbContextOptionsBuilder<HawalekDbContext>()
        .UseSqlServer(ConnectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "operations")).Options);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("HAWALEKNOW_TEST_SQL")
            ?? throw new InvalidOperationException("Set HAWALEKNOW_TEST_SQL to the isolated test SQL server connection with Database=master. No in-memory fallback is allowed.");
        var builder = new SqlConnectionStringBuilder(configured);
        if (!string.Equals(builder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test SQL bootstrap requires Database=master; application databases are forbidden.");
        masterConnection = builder.ConnectionString;
        builder.InitialCatalog = databaseName;
        ConnectionString = builder.ConnectionString;
        await using var connection = new SqlConnection(masterConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
        created = true;
        await using var context = CreateContext();
        if (InitialMigration is null) await context.Database.MigrateAsync();
        else await context.GetService<IMigrator>().MigrateAsync(InitialMigration);
    }

    public async Task DisposeAsync()
    {
        if (!created) return;
        // Both the random name and creation ownership are checked before any destructive SQL.
        if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, "^HN_P02_Test_[0-9a-f]{32}$"))
            throw new InvalidOperationException("Refusing cleanup of an unowned database.");
        SqlConnection.ClearPool(new SqlConnection(ConnectionString));
        await using var connection = new SqlConnection(masterConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
        await command.ExecuteNonQueryAsync();
    }
}
