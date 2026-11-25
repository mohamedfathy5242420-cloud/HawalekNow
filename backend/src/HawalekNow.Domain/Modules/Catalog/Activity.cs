namespace HawalekNow.Domain.Modules.Catalog;

public sealed class Activity
{
    public Guid Id { get; private set; }
    public string NameAr { get; private set; } = "";
    public string NameEn { get; private set; } = "";
    public string NameArKey { get; private set; } = "";
    public string NameEnKey { get; private set; } = "";
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Activity() { }
    public static Activity Create(string nameAr, string nameEn, bool isActive = true, Guid? id = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("A nonempty id is required.", nameof(id));
        var activity = new Activity { Id = id ?? Guid.CreateVersion7(), IsActive = isActive };
        activity.Rename(nameAr, nameEn);
        return activity;
    }

    public void Rename(string nameAr, string nameEn)
    {
        // Validate both names before changing either one.
        var ar = CatalogNames.Display(nameAr); var en = CatalogNames.Display(nameEn);
        var arKey = CatalogNames.Key(ar); var enKey = CatalogNames.Key(en);
        NameAr = ar; NameEn = en; NameArKey = arKey; NameEnKey = enKey;
    }
    public void SetActive(bool active) => IsActive = active;
}
