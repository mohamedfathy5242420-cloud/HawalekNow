using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Api.Shared.Errors;
using HawalekNow.Api.Shared.OpenApi;
using HawalekNow.Api.Shared.Routing;
using HawalekNow.Application.Shared;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSerilog(configuration => configuration.ConfigureSafePipelineLogging()
    .WriteTo.Console(outputTemplate: "{Timestamp:O} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"),
    preserveStaticLogger: true);
builder.Services.AddApplication();
builder.Services.AddApiErrorHandling().AddMvcOptions(options => options.Conventions.Add(new ApiV1RouteConvention()));
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer<ApiDocumentTransformer>());
var app = builder.Build();

app.UseApiErrorHandling();
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
