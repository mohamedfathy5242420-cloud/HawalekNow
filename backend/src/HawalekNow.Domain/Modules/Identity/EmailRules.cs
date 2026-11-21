using System.Net.Mail;

namespace HawalekNow.Domain.Modules.Identity;

public static class EmailRules
{
    public static bool IsValid(string? value) =>
        value is not null && value.Length <= 254 && !value.Any(char.IsControl) &&
        MailAddress.TryCreate(value, out var address) && address.DisplayName.Length == 0 &&
        string.Equals(address.Address, value, StringComparison.OrdinalIgnoreCase);
}
