namespace HawalekNow.Application.Shared.Diagnostics;

// Fixed templates are also the API sink's allowlist. Never add request/response/error text.
public static class PipelineLog
{
    public const string Started = "Request {RequestType} started";
    public const string Completed = "Request {RequestType} completed with {Outcome}";
    public const string Measured = "Request {RequestType} took {ElapsedMilliseconds} ms";
}
