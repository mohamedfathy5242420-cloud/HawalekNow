namespace HawalekNow.Domain.Modules.Identity;

public static class UsernameRules
{
    public static bool IsValid(string? value) => value is { Length: >= 3 and <= 64 } &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
}
