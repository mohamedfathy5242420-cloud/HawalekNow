using HawalekNow.Api.Shared.Diagnostics;
using HawalekNow.Application.Shared;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Services.AddSerilog(configuration => configuration.ConfigureSafePipelineLogging()
    .WriteTo.Console(outputTemplate: "{Timestamp:O} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}"));
builder.Services.AddApplication();
builder.Services.AddControllers();
var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.MapControllers();

app.Run();
