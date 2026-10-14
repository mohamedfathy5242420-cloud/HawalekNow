using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace HawalekNow.Api.Shared.Errors;

// MVC's automatic client errors also use this policy, independently of IProblemDetailsService.
public sealed class SafeProblemDetailsFactory : ProblemDetailsFactory
{
    public override ProblemDetails CreateProblemDetails(HttpContext httpContext, int? statusCode = null,
        string? title = null, string? type = null, string? detail = null, string? instance = null) =>
        ApiProblems.Create(httpContext, statusCode ?? 500);

    public override ValidationProblemDetails CreateValidationProblemDetails(HttpContext httpContext,
        ModelStateDictionary modelStateDictionary, int? statusCode = null, string? title = null,
        string? type = null, string? detail = null, string? instance = null)
    {
        var safe = ApiProblems.Create(httpContext, statusCode ?? 400);
        var result = new ValidationProblemDetails { Status = safe.Status, Title = safe.Title, Type = safe.Type };
        foreach (var extension in safe.Extensions) result.Extensions.Add(extension);
        return result;
    }
}
