namespace HawalekNow.Application.Shared.Results;

public sealed class Result
{
    public bool IsSuccess => Error is null;
    public Error? Error { get; }

    private Result(Error? error) => Error = error;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error ?? throw new ArgumentNullException(nameof(error)));
}

public sealed class Result<T>
{
    private readonly T? value;
    public bool IsSuccess => Error is null;
    public Error? Error { get; }
    public T Value => IsSuccess ? value! : throw new InvalidOperationException("A failed result has no value.");

    private Result(T? value, Error? error) { this.value = value; Error = error; }

    // Null is allowed when T permits it; success means the operation completed, not that a value exists.
    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error ?? throw new ArgumentNullException(nameof(error)));
}
