using HawalekNow.Api.Shared.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.Api.Shared.Errors;

public static class ApiErrorHandling
{
    public static IMvcBuilder AddApiErrorHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var safe = ApiProblems.Create(context.HttpContext, context.ProblemDetails.Status ?? 500);
            context.ProblemDetails.Title = safe.Title;
            context.ProblemDetails.Type = safe.Type;
            context.ProblemDetails.Detail = null;
            context.ProblemDetails.Instance = null;
            context.ProblemDetails.Extensions.Clear();
            foreach (var extension in safe.Extensions) context.ProblemDetails.Extensions.Add(extension);
        });
        var mvc = services.AddControllers().ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
                ApiProblems.AsObjectResult(ApiProblems.Create(context.HttpContext, 400));
        });
        services.Replace(ServiceDescriptor.Singleton<ProblemDetailsFactory, SafeProblemDetailsFactory>());
        return mvc;
    }

    public static IApplicationBuilder UseApiErrorHandling(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // Preserves S04's PipelineOnly policy; no exception telemetry schema is introduced.
            SuppressDiagnosticsCallback = _ => true
        });
        app.UseStatusCodePages(async context =>
        {
            if (context.HttpContext.RequestAborted.IsCancellationRequested) return;
            await ApiProblems.WriteAsync(context.HttpContext,
                ApiProblems.Create(context.HttpContext, context.HttpContext.Response.StatusCode),
                context.HttpContext.RequestAborted);
        });
        return app;
    }
}
