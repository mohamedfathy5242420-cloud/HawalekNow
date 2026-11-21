namespace HawalekNow.Domain.Modules.Identity;

public static class PasswordRules
{
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword", "password123456789", "123456789012345", "1234567890123456",
        "qwertyuiopasdfgh", "letmeinletmeinletmein", "administrator123", "aaaaaaaaaaaaaaa"
    };

    public static bool IsAcceptable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 15 or > 256 || Common.Contains(value)) return false;
        var characters = 0;
        foreach (var rune in value.EnumerateRunes()) { if (++characters > 128) return false; }
        return characters >= 15;
    }
}
