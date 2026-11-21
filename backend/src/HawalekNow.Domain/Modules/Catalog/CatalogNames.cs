using System.Text;

namespace HawalekNow.Domain.Modules.Catalog;

public static class CatalogNames
{
    public static string Display(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100 || value.Any(char.IsControl))
            throw new ArgumentException("A reference name must contain 1 to 100 visible characters.", nameof(value));
        return value.Trim();
    }

    public static string Key(string value)
    {
        var normalized = string.Join(' ', Display(value).Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        if (normalized.Length > 160) throw new ArgumentException("The normalized reference name is too long.", nameof(value));
        return normalized;
    }
}
