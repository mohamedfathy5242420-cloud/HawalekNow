using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace HawalekNow.Application.Shared.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<ValidationFailure> failures = [];
        // Sequential: validators may later share scoped, non-thread-safe dependencies.
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken);
            failures.AddRange(result.Errors);
        }
        if (failures.Count > 0) throw new ValidationException(failures);
        cancellationToken.ThrowIfCancellationRequested();
        return await next();
    }
}
