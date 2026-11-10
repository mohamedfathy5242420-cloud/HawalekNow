using HawalekNow.Application.Modules.Identity;
using HawalekNow.Domain.Modules.Identity;
using HawalekNow.Application.Shared.Messaging;
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
        services.AddScoped<AdminSeeder>();
        services.AddSingleton<IdentifierDigests>();
        services.AddScoped<IdentifierLocks>();
        services.AddSingleton<OtpProtection>();
        services.AddSingleton<SessionTokens>();
        services.AddSingleton<LoginTiming>();
        services.AddScoped<IAccountSessions, AccountSessions>();
        services.AddScoped<IdentityStateLock>();
        services.AddScoped<EmailConfirmationService>();
        services.AddScoped<IEmailConfirmation>(provider => provider.GetRequiredService<EmailConfirmationService>());
        services.AddScoped<IIdentityEmailTransport, MailpitEmailTransport>();
        services.AddScoped<IOutboxHandler, EmailOtpHandler>();
        services.AddScoped<IAccountRegistration, AccountRegistration>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHostedService<IdentityConfigurationValidation>();
        services.RemoveAll<IPasswordValidator<ApplicationUser>>();
        services.AddScoped<IPasswordValidator<ApplicationUser>, AccountPasswordValidator>();
        return services;
    }
}

public sealed class TypeScopedUserValidator(HawalekDbContext context, IdentifierDigests digests) : IUserValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var name = user.UserName;
        var email = user.Email;
        if (!UsernameRules.IsValid(name) || !EmailRules.IsValid(email))
            return Invalid();
        var normalizedName = manager.NormalizeName(name);
        var normalizedEmail = manager.NormalizeEmail(email);
        var nameDigest = digests.Hash(user.AccountType, IdentifierKind.Username, normalizedName!);
        var emailDigest = digests.Hash(user.AccountType, IdentifierKind.Email, normalizedEmail!);
        if (await context.Set<ReservedIdentifier>().AnyAsync(value => value.AccountType == user.AccountType &&
            ((value.Kind == IdentifierKind.Username && value.Digest == nameDigest) || (value.Kind == IdentifierKind.Email && value.Digest == emailDigest))))
            return Invalid();
        var exists = await context.Set<ApplicationUser>().AnyAsync(value => value.Id != user.Id && value.AccountType == user.AccountType &&
            (value.NormalizedUserName == normalizedName || value.NormalizedEmail == normalizedEmail));
        return exists ? Invalid() : IdentityResult.Success;
    }

    // No submitted identity, provider details, or existence reason leaves the storage abstraction.
    private static IdentityResult Invalid() => IdentityResult.Failed(new IdentityError { Code = "InvalidAccount", Description = "Account details are invalid." });
}

public sealed class AccountPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password) =>
        Task.FromResult(PasswordRules.IsAcceptable(password) ? IdentityResult.Success :
            IdentityResult.Failed(new IdentityError { Code = "InvalidPassword", Description = "The password does not satisfy the password policy." }));
}
