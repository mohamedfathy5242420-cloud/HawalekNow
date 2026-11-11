using System.Globalization;
using System.Threading.RateLimiting;
using HawalekNow.Api.Shared.Errors;

namespace HawalekNow.Api.Modules.Identity;

public static class IdentityRateLimits
{
    public const string Registration = "identity-signup";
    public const string Login = "identity-login";
    public const string SendCode = "identity-send-code";
    public const string VerifyCode = "identity-verify-code";
    public const string Refresh = "identity-refresh";
    public const string SessionMutation = "identity-session-mutation";
    public static IServiceCollection AddIdentityRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            foreach (var (name, count, window) in new[] { (Registration, 5, TimeSpan.FromMinutes(10)), (Login, 10, TimeSpan.FromMinutes(1)),
                (SendCode, 5, TimeSpan.FromMinutes(15)), (VerifyCode, 20, TimeSpan.FromMinutes(10)), (Refresh, 30, TimeSpan.FromMinutes(1)), (SessionMutation, 30, TimeSpan.FromMinutes(1)) })
                options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown-peer", _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = count, Window = window, QueueLimit = 0, AutoReplenishment = true }));
            options.OnRejected = (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retry.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return new ValueTask(ApiProblems.WriteAsync(context.HttpContext, ApiProblems.Create(context.HttpContext, 429), cancellationToken));
            };
        });
        return services;
    }
}
