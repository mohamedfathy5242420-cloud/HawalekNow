using HawalekNow.Infrastructure.Modules.Search;
using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Api.Modules.Catalog;
using HawalekNow.Api.Shared.Security;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.OpenApi;
using HawalekNow.Api.Shared.Routing;
using HawalekNow.Application.Shared;
using HawalekNow.Application.Modules.Locations;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Catalog;
using HawalekNow.Infrastructure.Modules.Locations;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Messaging;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

if (args.Contains("--seed-reference-data", StringComparer.Ordinal))
{
    try
    {
        if (args.Contains("--seed-admin", StringComparer.Ordinal)) throw new InvalidOperationException("Choose one explicit bootstrap command.");
        var bootstrap = Host.CreateApplicationBuilder(args.Where(argument => argument != "--seed-reference-data").ToArray());
        bootstrap.Logging.ClearProviders();
        bootstrap.Services.AddPersistence(bootstrap.Configuration).AddCatalogReferenceData().AddLocationsReferenceData();
        using var bootstrapHost = bootstrap.Build(); await using var scope = bootstrapHost.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IReferenceDataSeeder>().SeedAsync();
        Console.WriteLine($"Reference seed completed. Added activities={result.ActivitiesAdded}; governorates={result.GovernoratesAdded}; cities={result.CitiesAdded}; areas={result.AreasAdded}. Existing Admin data preserved; nationwide completeness not claimed.");
    }
    catch (Exception)
    {
        Console.Error.WriteLine("Reference seed failed. Check the reviewed database configuration and pinned dataset."); Environment.ExitCode = 1;
    }
    return;
}

if (args.Contains("--seed-admin", StringComparer.Ordinal))
{
    try
    {
        var bootstrap = Host.CreateApplicationBuilder(args.Where(argument => argument != "--seed-admin").ToArray());
        bootstrap.Logging.ClearProviders();
        var connection = bootstrap.Configuration.GetConnectionString("Application")
            ?? throw new InvalidOperationException("Configure the application database before explicit bootstrap.");
        bootstrap.Services.AddPersistence(connection).AddIdentityPersistence().AddMerchantAccounts().AddBackgroundProcessing();
        using var bootstrapHost = bootstrap.Build();
        await using var scope = bootstrapHost.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync(AdminSeedSettings.Read(bootstrap.Configuration));
        Console.WriteLine("Admin bootstrap completed. Existing accounts were preserved.");
    }
    catch (Exception)
    {
        // Bootstrap is an explicit operator command, not an HTTP endpoint. Never print settings or exception details.
        Console.Error.WriteLine("Admin bootstrap failed. Check local configuration and database availability.");
        Environment.ExitCode = 1;
    }
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSerilog(configuration => configuration.ConfigureSafePipelineLogging()
    .WriteTo.Console(outputTemplate: "{Timestamp:O} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"),
    preserveStaticLogger: true);
builder.Services.AddApplication();
builder.Services.AddIdentityAuthentication();
builder.Services.AddAccountPolicies();
builder.Services.AddBrowserSecurity().AddIdentityRateLimits();
builder.Services.AddReferenceDataRateLimits();
builder.Services.AddMerchantRateLimits();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<SessionTokens, TimeProvider>(
    (options, tokens, clock) => options.TokenValidationParameters = tokens.Validation(clock));
builder.Services.AddPersistence(builder.Configuration).AddIdentityPersistence().AddMerchantAccounts().AddMerchantStores().AddBackgroundProcessing();
builder.Services.AddCatalogReferenceData();
builder.Services.AddLocationsReferenceData();
builder.Services.AddSearchReferenceData();
builder.Services.AddApiErrorHandling().AddMvcOptions(options => options.Conventions.Add(new ApiV1RouteConvention()));
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer<ApiDocumentTransformer>());
var app = builder.Build();

app.UseApiErrorHandling();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/identity") || context.User.Identity?.IsAuthenticated == true) context.Response.Headers.CacheControl = "no-store";
    await next(context);
});
if (app.Environment.IsDevelopment())
{
    // Only this document exists. Let unknown names fall through to the existing safe 404 policy.
    app.MapOpenApi("/openapi/{documentName:regex(^v1$)}.json");
    app.MapScalarApiReference(options => options.WithTitle("HawalekNow API").DisableDefaultFonts());
}
app.MapControllers();

app.Run();

// WebApplicationFactory boots this production entry point, including its registrations and environment gates.
public partial class Program;
