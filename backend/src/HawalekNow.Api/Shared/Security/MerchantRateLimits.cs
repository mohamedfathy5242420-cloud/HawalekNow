using System.Threading.RateLimiting;

namespace HawalekNow.Api.Shared.Security;

public static class MerchantRateLimits
{
    public const string Mutation = "merchant-mutation";
    public static IServiceCollection AddMerchantRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options => options.AddPolicy(Mutation, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown-peer", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
        return services;
    }
}
