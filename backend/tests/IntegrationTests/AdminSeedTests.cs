using System.Diagnostics;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

public sealed class AdminSeedTests
{
    private static ServiceProvider Services(string connection)
    {
        var services = new ServiceCollection();
        services.AddLogging().AddPersistence(connection).AddIdentityPersistence();
        return services.BuildServiceProvider();
    }

    private static AdminSeedSettings Settings() => new()
    {
        Username = "admin" + Guid.NewGuid().ToString("N"),
        Email = Guid.NewGuid().ToString("N") + "@example.invalid",
        Password = "Test-only-" + Guid.NewGuid().ToString("N")
    };

    [Fact]
    public async Task Concurrent_bootstraps_create_one_admin_and_never_reset_credentials_or_identity()
    {
        await using var database = new SqlDatabase();
        await database.InitializeAsync();
        await using var provider = Services(database.ConnectionString);
        var first = Settings();
        async Task<AdminSeedOutcome> Seed(AdminSeedSettings settings)
        {
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync(settings);
        }
        var results = await Task.WhenAll(Seed(first), Seed(first));
        Assert.Single(results, result => result == AdminSeedOutcome.Created);
        Assert.Single(results, result => result == AdminSeedOutcome.Unchanged);
        await using var context = database.CreateContext();
        var original = await context.Set<ApplicationUser>().AsNoTracking().SingleAsync();
        var changed = Settings();
        Assert.Equal(AdminSeedOutcome.Unchanged, await Seed(changed));
        var after = await context.Set<ApplicationUser>().AsNoTracking().SingleAsync();
        Assert.Equal(original.Id, after.Id);
        Assert.Equal(original.UserName, after.UserName);
        Assert.Equal(original.Email, after.Email);
        Assert.Equal(original.PasswordHash, after.PasswordHash);
        Assert.Equal(original.SecurityStamp, after.SecurityStamp);
        Assert.Equal(original.RowVersion, after.RowVersion);
        Assert.Equal(AccountType.Admin, after.AccountType);
        Assert.True(after.EmailConfirmed);
        await using var verify = provider.CreateAsyncScope();
        var manager = verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await manager.CheckPasswordAsync(after, first.Password!));
        Assert.False(await manager.CheckPasswordAsync(after, changed.Password!));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("email")]
    [InlineData("password")]
    [InlineData("username")]
    public async Task Missing_or_invalid_configuration_fails_without_creating_accounts_or_echoing_secrets(string failure)
    {
        await using var database = new SqlDatabase();
        await database.InitializeAsync();
        await using var provider = Services(database.ConnectionString);
        await using var scope = provider.CreateAsyncScope();
        var valid = Settings();
        var settings = new AdminSeedSettings
        {
            Username = failure == "username" ? "invalid@name" : valid.Username,
            Email = failure == "email" ? "invalid" : valid.Email,
            Password = failure == "missing" ? null : failure == "password" ? "short" : valid.Password
        };
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync(settings));
        Assert.Equal("Admin bootstrap configuration is missing or invalid.", exception.Message);
        Assert.DoesNotContain(valid.Password!, exception.ToString());
        await using var context = database.CreateContext();
        Assert.False(await context.Set<ApplicationUser>().AnyAsync());
    }

    [Fact]
    public async Task Actual_Program_bootstrap_command_runs_twice_and_rejects_missing_configuration_without_secret_output()
    {
        await using var database = new SqlDatabase();
        await database.InitializeAsync();
        var settings = Settings();
        async Task<(int Exit, string Output)> Run(bool includePassword)
        {
            var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add(typeof(Program).Assembly.Location);
            info.ArgumentList.Add("--seed-admin");
            info.Environment["ConnectionStrings__Application"] = database.ConnectionString;
            info.Environment["Identity__AdminSeed__Username"] = settings.Username;
            info.Environment["Identity__AdminSeed__Email"] = settings.Email;
            info.Environment.Remove("Identity__AdminSeed__Password");
            if (includePassword) info.Environment["Identity__AdminSeed__Password"] = settings.Password;
            using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start bootstrap test process.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            var output = await stdout + await stderr;
            Assert.DoesNotContain(settings.Password!, output);
            Assert.DoesNotContain(settings.Email!, output);
            Assert.DoesNotContain(settings.Username!, output);
            return (process.ExitCode, output);
        }
        Assert.Equal(0, (await Run(true)).Exit);
        Assert.Equal(0, (await Run(true)).Exit);
        Assert.Equal(1, (await Run(false)).Exit);
        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Set<ApplicationUser>().CountAsync(user => user.AccountType == AccountType.Admin));
    }
}
