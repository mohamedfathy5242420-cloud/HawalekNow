using HawalekNow.Api.Shared.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace HawalekNow.Api.Shared.Security;

public static class BrowserSecurity
{
    public const string CsrfCookie = "__Host-HawalekNow.Csrf";
    public const string CsrfHeader = "X-CSRF-TOKEN";
    public static IServiceCollection AddBrowserSecurity(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName("HawalekNow");
        // Resolve final configuration through DI, including the host's configuration providers.
        services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, IHostEnvironment, ILoggerFactory>((options, configuration, environment, logs) =>
        {
            var path = configuration["Security:KeyRingPath"];
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("Security:KeyRingPath must be absolute and outside source control.");
                options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(path), logs);
                if (OperatingSystem.IsWindows()) options.XmlEncryptor = new DpapiXmlEncryptor(protectToLocalMachine: false, logs);
            }
            else if (!environment.IsDevelopment()) throw new InvalidOperationException("Configure a protected persistent Security:KeyRingPath outside source control.");
        });
        services.AddHostedService<BrowserKeyConfigurationValidation>();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeader;
            options.Cookie.Name = CsrfCookie; options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict; options.Cookie.IsEssential = true;
        });
        services.AddScoped<BrowserAntiforgeryFilter>();
        services.Configure<MvcOptions>(options => options.Filters.AddService<BrowserAntiforgeryFilter>());
        return services;
    }
}

public sealed class BrowserKeyConfigurationValidation(IOptions<KeyManagementOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) { _ = options.Value; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// AddControllers does not register the ViewFeatures implementation behind AutoValidateAntiforgeryToken.
// This API filter calls the same framework verifier directly and keeps the approved safe error contract.
public sealed class BrowserAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order => 1000;
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method)) return;
        if (!context.HttpContext.Request.IsHttps)
        {
            context.Result = ApiProblems.AsObjectResult(ApiProblems.Create(context.HttpContext, 403)); return;
        }
        try { await antiforgery.ValidateRequestAsync(context.HttpContext); }
        catch (AntiforgeryValidationException)
        {
            context.Result = ApiProblems.AsObjectResult(ApiProblems.Create(context.HttpContext, 400));
        }
    }
}
