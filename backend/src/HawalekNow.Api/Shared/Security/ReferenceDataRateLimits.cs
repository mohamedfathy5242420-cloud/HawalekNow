using System.Threading.RateLimiting;

namespace HawalekNow.Api.Shared.Security;

public static class ReferenceDataRateLimits
{
    public const string Mutation = "reference-data-mutation";
    public static IServiceCollection AddReferenceDataRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options => options.AddPolicy(Mutation, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown-peer", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
        return services;
    }
}
