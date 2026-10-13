namespace HawalekNow.Application.Shared.Diagnostics;

// Scoped: HTTP middleware initializes it; non-HTTP Send calls get a safe fallback.
public sealed class CorrelationContext
{
    public string Id { get; private set; } = Guid.NewGuid().ToString("N");

    public void Initialize(string? incoming) => Id = IsValid(incoming) ? incoming! : Guid.NewGuid().ToString("N");

    public static bool IsValid(string? value) => value is { Length: >= 1 and <= 64 } &&
        value.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or '-' or '_');
}
