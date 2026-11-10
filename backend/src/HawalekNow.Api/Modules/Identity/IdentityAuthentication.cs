using System.Security.Claims;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Application.Modules.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace HawalekNow.Api.Modules.Identity;

public static class IdentityAuthentication
{
    public const string AccessCookie = "__Host-HawalekNow.Access";
    public const string RefreshCookie = "__Host-HawalekNow.Refresh";
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure(options =>
        {
            options.MapInboundClaims = false; options.SaveToken = false; options.IncludeErrorDetails = false;
            options.Events = new()
            {
                OnMessageReceived = context =>
                {
                    // No Authorization-header fallback: this closed application uses cookies only.
                    if (!context.Request.IsHttps || !context.Request.Cookies.TryGetValue(AccessCookie, out var cookie) || string.IsNullOrWhiteSpace(cookie)) context.NoResult();
                    else context.Token = cookie;
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    if (!Guid.TryParseExact(context.Principal?.FindFirstValue("sub"), "N", out var userId) ||
                        !Guid.TryParseExact(context.Principal?.FindFirstValue("sid"), "N", out var sessionId)) { context.Fail("Invalid session."); return; }
                    var actor = await context.HttpContext.RequestServices.GetRequiredService<IAccountSessions>().FindActiveAsync(userId, sessionId, context.HttpContext.RequestAborted);
                    if (actor is null) { context.Fail("Invalid session."); return; }
                    // Only database state supplies authorization claims. Incoming token roles are discarded.
                    context.Principal = new(new ClaimsIdentity([new("sub", actor.UserId.ToString("N")), new("sid", actor.SessionId.ToString("N")),
                        new("account_type", actor.AccountType), new("email_confirmed", actor.EmailConfirmed ? "true" : "false")],
                        JwtBearerDefaults.AuthenticationScheme, "sub", "account_type"));
                },
                OnChallenge = context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = 401;
                    return ApiProblems.WriteAsync(context.HttpContext, ApiProblems.Create(context.HttpContext, 401), context.HttpContext.RequestAborted);
                },
                OnForbidden = context =>
                {
                    context.Response.StatusCode = 403;
                    return ApiProblems.WriteAsync(context.HttpContext, ApiProblems.Create(context.HttpContext, 403), context.HttpContext.RequestAborted);
                }
            };
        });
        return services;
    }
    public static void WriteCookies(HttpResponse response, SessionCredentials credentials)
    {
        response.Headers.CacheControl = "no-store";
        response.Cookies.Append(AccessCookie, credentials.AccessToken, Options(credentials.AccessExpiresAt));
        response.Cookies.Append(RefreshCookie, credentials.RefreshToken, Options(credentials.RefreshExpiresAt));
    }
    public static void ClearCookies(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Cookies.Delete(AccessCookie, Options(null)); response.Cookies.Delete(RefreshCookie, Options(null));
    }
    private static CookieOptions Options(DateTimeOffset? expiry) => new()
    { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true, Expires = expiry };
}
