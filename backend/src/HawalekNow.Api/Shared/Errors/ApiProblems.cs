using HawalekNow.Application.Shared.Diagnostics;
using HawalekNow.Application.Shared.Results;
using Microsoft.AspNetCore.Mvc;

namespace HawalekNow.Api.Shared.Errors;

public static class ApiProblems
{
    public static ProblemDetails FromError(HttpContext context, Error error) => Create(context, error.Kind switch
    {
        ErrorKind.NotFound => 404,
        ErrorKind.Conflict => 409,
        ErrorKind.Validation => 400,
        ErrorKind.Unauthorized => 401,
        ErrorKind.Forbidden => 403,
        _ => throw new ArgumentOutOfRangeException(nameof(error))
    });

    public static ProblemDetails Create(HttpContext context, int status)
    {
        var (title, code) = status switch
        {
            400 => ("The request is invalid.", "request.invalid"),
            401 => ("Authentication is required.", "request.unauthorized"),
            403 => ("Access is forbidden.", "request.forbidden"),
            404 => ("The resource was not found.", "resource.not_found"),
            405 => ("The method is not allowed.", "request.method_not_allowed"),
            409 => ("The request conflicts with the current state.", "resource.conflict"),
            415 => ("The media type is not supported.", "request.unsupported_media_type"),
            < 500 => ("The request could not be completed.", "request.failed"),
            _ => ("An unexpected error occurred.", "server.error")
        };
        return new ProblemDetails
        {
            Status = status, Title = title, Type = "about:blank",
            Extensions =
            {
                ["code"] = code,
                ["correlationId"] = context.RequestServices.GetRequiredService<CorrelationContext>().Id
            }
        };
    }

    public static ObjectResult AsObjectResult(ProblemDetails problem)
    {
        var result = new ObjectResult(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    public static IActionResult ToActionResult(this Result result, HttpContext context) =>
        result.IsSuccess ? new NoContentResult() : AsObjectResult(FromError(context, result.Error!));

    public static IActionResult ToActionResult<T>(this Result<T> result, HttpContext context)
    {
        if (!result.IsSuccess) return AsObjectResult(FromError(context, result.Error!));
        var value = result.Value;
        // MVC's no-content formatter turns OkObjectResult(null) into 204. Preserve the Result<T> contract locally.
        return value is null
            ? new JsonResult(null) { StatusCode = StatusCodes.Status200OK, ContentType = "application/json" }
            : new OkObjectResult(value);
    }

    public static Task WriteAsync(HttpContext context, ProblemDetails problem, CancellationToken token) =>
        context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: token);
}
