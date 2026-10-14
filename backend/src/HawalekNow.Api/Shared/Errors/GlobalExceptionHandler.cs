using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace HawalekNow.Api.Shared.Errors;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.Response.HasStarted) return false;
        if (context.RequestAborted.IsCancellationRequested && exception is OperationCanceledException or IOException)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        // Never inspect FluentValidation messages, property names, ErrorCode, CustomState or AttemptedValue.
        // Until approved public validation rules exist, every validation rejection uses one safe code.
        var status = exception is ValidationException ? 400 : 500;
        context.Response.StatusCode = status;
        await ApiProblems.WriteAsync(context, ApiProblems.Create(context, status), cancellationToken);
        return true;
    }
}
