using HawalekNow.Application.Shared.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace HawalekNow.Api.Shared.Diagnostics;

// S04 emits only the three pipeline events. Expanding this policy requires a reviewed schema.
public sealed class SafePipelineLogFilter : ILogEventFilter
{
    public bool IsEnabled(LogEvent logEvent)
    {
        if (logEvent.Exception is not null) return false;
        var template = logEvent.MessageTemplate.Text;
        if (template is not (PipelineLog.Started or PipelineLog.Completed or PipelineLog.Measured)) return false;
        string[] required = template switch
        {
            PipelineLog.Completed => ["SourceContext", "RequestType", "CorrelationId", "Outcome"],
            PipelineLog.Measured => ["SourceContext", "RequestType", "CorrelationId", "ElapsedMilliseconds"],
            _ => ["SourceContext", "RequestType", "CorrelationId"]
        };
        if (!logEvent.Properties.Keys.Order().SequenceEqual(required.Order())) return false;
        if (!TryString(logEvent, "SourceContext", out var source) ||
            !source.StartsWith("HawalekNow.Application.Shared.Behaviors.", StringComparison.Ordinal)) return false;
        if (!TryString(logEvent, "CorrelationId", out var correlation) || !CorrelationContext.IsValid(correlation)) return false;
        if (!TryString(logEvent, "RequestType", out var requestType) || requestType.Length is < 1 or > 200 ||
            requestType.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '`'))) return false;
        if (template == PipelineLog.Completed && (!TryString(logEvent, "Outcome", out var outcome) ||
            outcome is not ("Succeeded" or "Failed" or "Canceled"))) return false;
        if (template == PipelineLog.Measured && (!logEvent.Properties.TryGetValue("ElapsedMilliseconds", out var elapsed) ||
            elapsed is not ScalarValue { Value: double milliseconds } || !double.IsFinite(milliseconds) || milliseconds < 0)) return false;
        return true;
    }

    private static bool TryString(LogEvent logEvent, string name, out string value)
    {
        value = logEvent.Properties.TryGetValue(name, out var property) && property is ScalarValue { Value: string text }
            ? text : "";
        return value.Length > 0;
    }
}
