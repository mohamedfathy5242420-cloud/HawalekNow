using System.ComponentModel.DataAnnotations;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class AdminSeedSettings
{
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }

    public static AdminSeedSettings Read(IConfiguration configuration) => new()
    {
        Username = configuration["Identity:AdminSeed:Username"],
        Email = configuration["Identity:AdminSeed:Email"],
        Password = configuration["Identity:AdminSeed:Password"]
    };

    public void Validate()
    {
        if (Username is null || Username.Trim().Length is < 3 or > 64 ||
            Username.Trim().Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-') ||
            Email is null || Email.Trim().Length > 254 || !new EmailAddressAttribute().IsValid(Email.Trim()) || Password is null || Password.Length is < 15 or > 128)
            throw new InvalidOperationException("Admin bootstrap configuration is missing or invalid.");
    }
}

public enum AdminSeedOutcome { Created, Unchanged }

public sealed class AdminSeeder(HawalekDbContext context, UserManager<ApplicationUser> manager)
{
    public async Task<AdminSeedOutcome> SeedAsync(AdminSeedSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate(); // Validate before touching SQL; never include submitted settings in an exception.
        await using var transaction = await context.BeginAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'HawalekNow.Identity.AdminBootstrap',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=10000;
            IF @result < 0 THROW 51001, N'Admin bootstrap lock unavailable.', 1;
            """, cancellationToken);
        // Bootstrap creates the first administrator only. Changed settings are never an account-update operation.
        if (await context.Set<ApplicationUser>().AnyAsync(user => user.AccountType == AccountType.Admin, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return AdminSeedOutcome.Unchanged;
        }
        var user = ApplicationUser.Create(AccountType.Admin, settings.Username!, settings.Email!);
        user.EmailConfirmed = true; // Trusted, explicit operator bootstrap; public registration cannot do this.
        user.LockoutEnabled = true;
        var created = await manager.CreateAsync(user, settings.Password!);
        if (!created.Succeeded) throw new InvalidOperationException("Admin bootstrap could not create an account.");
        await transaction.CommitAsync(cancellationToken);
        return AdminSeedOutcome.Created;
    }
}
