namespace HawalekNow.Application.Shared.Results;

// A closed technical vocabulary: no exception messages, input values or HTTP metadata.
public enum ErrorKind { NotFound, Conflict, Validation, Unauthorized, Forbidden }

public sealed record Error
{
    public ErrorKind Kind { get; }

    public Error(ErrorKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
    }
}
