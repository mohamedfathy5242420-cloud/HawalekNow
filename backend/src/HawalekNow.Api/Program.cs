using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Api.Modules.Identity;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.OpenApi;
using HawalekNow.Api.Shared.Routing;
using HawalekNow.Application.Shared;
using HawalekNow.Infrastructure.Modules.Identity;
using HawalekNow.Infrastructure.Modules.Merchants;
using HawalekNow.Infrastructure.Shared.Persistence;
using HawalekNow.Infrastructure.Shared.Messaging;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Serilog;

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
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<SessionTokens, TimeProvider>(
    (options, tokens, clock) => options.TokenValidationParameters = tokens.Validation(clock));
builder.Services.AddPersistence(builder.Configuration).AddIdentityPersistence().AddMerchantAccounts().AddBackgroundProcessing();
builder.Services.AddApiErrorHandling().AddMvcOptions(options => options.Conventions.Add(new ApiV1RouteConvention()));
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer<ApiDocumentTransformer>());
var app = builder.Build();

app.UseApiErrorHandling();
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
