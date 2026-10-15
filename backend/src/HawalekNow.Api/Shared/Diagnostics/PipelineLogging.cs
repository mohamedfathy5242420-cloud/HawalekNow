using Serilog;

namespace HawalekNow.Api.Shared.Diagnostics;

public static class PipelineLogging
{
    public static LoggerConfiguration ConfigureSafePipelineLogging(this LoggerConfiguration configuration) =>
        configuration.MinimumLevel.Information().Enrich.FromLogContext().Enrich.With<RemoveHostMetadata>()
            .Filter.With<SafePipelineLogFilter>();
}
