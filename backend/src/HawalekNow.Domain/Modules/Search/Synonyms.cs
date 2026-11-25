using System.Text;

namespace HawalekNow.Domain.Modules.Search;

public static class SynonymText
{
    public static string Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100 || value.Any(char.IsControl)) throw new ArgumentException("Invalid synonym group name.");
        return value.Trim();
    }
    public static string Key(string original)
    {
        if (string.IsNullOrWhiteSpace(original) || original.Length > 80 || original.Any(value => char.IsControl(value) && !char.IsWhiteSpace(value))) throw new ArgumentException("Invalid synonym term.");
        var key = string.Join(' ', original.Normalize(NormalizationForm.FormKC).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        if (key.Length is 0 or > 256) throw new ArgumentException("Invalid normalized synonym term.");
        return key;
    }
    public static string[] Keys(IEnumerable<string> originals)
    {
        var keys = originals.Select(Key).ToArray();
        if (keys.Length is < 1 or > 100 || keys.Distinct(StringComparer.Ordinal).Count() != keys.Length) throw new ArgumentException("Invalid or duplicate group terms.");
        return keys;
    }
}
public sealed class SynonymGroup
{
    public Guid Id { get; private set; }
    public string NameAr { get; private set; } = "";
    public string NameEn { get; private set; } = "";
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private SynonymGroup() { }
    public static SynonymGroup Create(string ar, string en, bool active) { var group = new SynonymGroup { Id = Guid.CreateVersion7() }; group.Edit(ar, en, active); return group; }
    public void Edit(string ar, string en, bool active) { var validAr = SynonymText.Name(ar); var validEn = SynonymText.Name(en); NameAr = validAr; NameEn = validEn; IsActive = active; }
}
public sealed class SynonymTerm
{
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public string Original { get; private set; } = "";
    public string NormalizedKey { get; private set; } = "";
    private SynonymTerm() { }
    public static SynonymTerm Create(Guid groupId, string original)
    {
        if (groupId == Guid.Empty) throw new ArgumentException("A group is required.");
        return new() { Id = Guid.CreateVersion7(), GroupId = groupId, Original = original, NormalizedKey = SynonymText.Key(original) };
    }
}
