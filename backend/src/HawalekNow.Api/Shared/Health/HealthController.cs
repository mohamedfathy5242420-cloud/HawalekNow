using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HawalekNow.Api.Shared.Health;

[ApiController]
[Route("health")]
[Tags("Health")]
public sealed class HealthController(HealthCheckService healthChecks) : ControllerBase
{
    [HttpGet("live")]
    [EndpointSummary("Application liveness")]
    [EndpointDescription("Basic application liveness only. No SQL, email or dependency readiness checks are registered.")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/plain")]
    [ProducesResponseType<string>(StatusCodes.Status503ServiceUnavailable, "text/plain")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ContentResult> Live(CancellationToken cancellationToken)
    {
        // Only checks explicitly tagged for liveness may affect this endpoint. An empty set is the basic
        // framework liveness probe: reaching this action proves the application can process a request.
        var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("live"), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return new ContentResult
        {
            StatusCode = report.Status == HealthStatus.Unhealthy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK,
            ContentType = "text/plain; charset=utf-8",
            Content = report.Status.ToString()
        };
    }
}
