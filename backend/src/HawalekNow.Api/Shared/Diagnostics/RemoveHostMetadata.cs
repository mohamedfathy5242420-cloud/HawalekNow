using Serilog.Core;
using Serilog.Events;

namespace HawalekNow.Api.Shared.Diagnostics;

// ASP.NET adds these ambient properties to ILogger scopes even when host events are filtered out.
// Discard them before the unchanged allowlist checks the Pipeline schema; never emit HTTP metadata.
public sealed class RemoveHostMetadata : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var key in new[] { "RequestId", "RequestPath", "ConnectionId", "ActionId", "ActionName" })
            logEvent.RemovePropertyIfPresent(key);
    }
}
