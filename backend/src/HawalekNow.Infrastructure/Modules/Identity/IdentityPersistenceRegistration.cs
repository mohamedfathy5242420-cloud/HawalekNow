using System.ComponentModel.DataAnnotations;
using HawalekNow.Infrastructure.Shared.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.Infrastructure.Modules.Identity;

public static class IdentityPersistenceRegistration
{
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services)
    {
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = false; // The custom validator and SQL enforce uniqueness inside AccountType.
            options.Password.RequiredLength = 15;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddUserStore<UserOnlyStore<ApplicationUser, HawalekDbContext, Guid>>();
        services.RemoveAll<IUserValidator<ApplicationUser>>();
        services.AddScoped<IUserValidator<ApplicationUser>, TypeScopedUserValidator>();
        return services;
    }
}

public sealed class TypeScopedUserValidator(HawalekDbContext context) : IUserValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var name = user.UserName;
        var email = user.Email;
        if (name is null || name.Length is < 3 or > 64 || name.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-') ||
            email is null || email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            return Invalid();
        var normalizedName = manager.NormalizeName(name);
        var normalizedEmail = manager.NormalizeEmail(email);
        var exists = await context.Set<ApplicationUser>().AnyAsync(value => value.Id != user.Id && value.AccountType == user.AccountType &&
            (value.NormalizedUserName == normalizedName || value.NormalizedEmail == normalizedEmail));
        return exists ? Invalid() : IdentityResult.Success;
    }

    // No submitted identity, provider details, or existence reason leaves the storage abstraction.
    private static IdentityResult Invalid() => IdentityResult.Failed(new IdentityError { Code = "InvalidAccount", Description = "Account details are invalid." });
}
