using System.Security.Claims;
using HawalekNow.Application.Modules.Identity.Contracts;
using HawalekNow.Application.Modules.Merchants.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace HawalekNow.Api.Modules.Identity;

public static class AccountPolicies
{
    public static IServiceCollection AddAccountPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(IdentityPolicies.AdminOnly, policy => policy.RequireAuthenticatedUser().RequireClaim("account_type", "Admin").RequireClaim("email_confirmed", "true"));
            options.AddPolicy(IdentityPolicies.VerifiedCustomer, policy => policy.RequireAuthenticatedUser().RequireClaim("account_type", "Customer").RequireClaim("email_confirmed", "true"));
            options.AddPolicy(IdentityPolicies.VerifiedMerchantSetup, policy => policy.RequireAuthenticatedUser().RequireClaim("account_type", "Merchant").RequireClaim("email_confirmed", "true").AddRequirements(new MerchantRequirement(false)));
            options.AddPolicy(IdentityPolicies.ApprovedMerchant, policy => policy.RequireAuthenticatedUser().RequireClaim("account_type", "Merchant").RequireClaim("email_confirmed", "true").AddRequirements(new MerchantRequirement(true)));
        });
        services.AddScoped<IAuthorizationHandler, MerchantAuthorizationHandler>(); return services;
    }
}
public sealed record MerchantRequirement(bool ApprovedOnly) : IAuthorizationRequirement;
public sealed class MerchantAuthorizationHandler(IMerchantAccounts merchants) : AuthorizationHandler<MerchantRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MerchantRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || context.User.FindFirstValue("account_type") != "Merchant" ||
            context.User.FindFirstValue("email_confirmed") != "true" || !Guid.TryParseExact(context.User.FindFirstValue("sub"), "N", out var owner)) return;
        var account = await merchants.FindByOwnerAsync(owner, context.Resource is HttpContext http ? http.RequestAborted : CancellationToken.None);
        if (account is not null && !account.IsSuspended && (requirement.ApprovedOnly ? account.IsApproved : account.CanPrepareAccount)) context.Succeed(requirement);
    }
}
