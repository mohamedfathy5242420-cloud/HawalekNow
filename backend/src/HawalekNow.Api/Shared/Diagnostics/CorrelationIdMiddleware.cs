using HawalekNow.Application.Shared.Diagnostics;

namespace HawalekNow.Api.Shared.Diagnostics;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, CorrelationContext correlation)
    {
        var incoming = context.Request.Headers[HeaderName];
        correlation.Initialize(incoming.Count == 1 ? incoming[0] : null);
        var id = correlation.Id;
        context.Response.Headers[HeaderName] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });
        await next(context);
    }
}
