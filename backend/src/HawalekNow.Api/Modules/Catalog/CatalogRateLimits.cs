using System.Threading.RateLimiting;

namespace HawalekNow.Api.Modules.Catalog;

public static class CatalogRateLimits
{
    public const string Mutation = "catalog-reference-mutation";
    public static IServiceCollection AddCatalogRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options => options.AddPolicy(Mutation, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown-peer", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true })));
        return services;
    }
}
