using System.Diagnostics;
using HawalekNow.Application.Shared.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HawalekNow.Application.Shared.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        try { return await next(); }
        finally
        {
            logger.LogInformation(PipelineLog.Measured, typeof(TRequest).Name,
                Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }
}
