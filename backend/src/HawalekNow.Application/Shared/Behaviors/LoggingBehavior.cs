using HawalekNow.Application.Shared.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HawalekNow.Application.Shared.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    CorrelationContext correlation) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlation.Id });
        logger.LogInformation(PipelineLog.Started, typeof(TRequest).Name);
        var outcome = "Failed";
        try
        {
            var response = await next();
            outcome = "Succeeded";
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "Canceled";
            throw;
        }
        finally
        {
            // No exception object/message: validation errors can contain attempted secret values.
            logger.LogInformation(PipelineLog.Completed, typeof(TRequest).Name, outcome);
        }
    }
}
