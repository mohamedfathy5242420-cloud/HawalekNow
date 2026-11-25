namespace HawalekNow.Domain.Modules.Catalog;

public sealed class Category
{
    public Guid Id { get; private set; }
    public Guid ActivityId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string NameAr { get; private set; } = "";
    public string NameEn { get; private set; } = "";
    public string NameArKey { get; private set; } = "";
    public string NameEnKey { get; private set; } = "";
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private Category() { }
    public static Category Create(Guid activityId, Guid? parentId, string nameAr, string nameEn, bool isActive = true, Guid? id = null)
    {
        if (activityId == Guid.Empty || id == Guid.Empty) throw new ArgumentException("Nonempty reference ids are required.");
        var category = new Category { Id = id ?? Guid.CreateVersion7(), ActivityId = activityId, IsActive = isActive };
        category.SetParent(parentId); category.Rename(nameAr, nameEn);
        return category;
    }
    public void Rename(string nameAr, string nameEn)
    {
        var ar = CatalogNames.Display(nameAr); var en = CatalogNames.Display(nameEn);
        var arKey = CatalogNames.Key(ar); var enKey = CatalogNames.Key(en);
        NameAr = ar; NameEn = en; NameArKey = arKey; NameEnKey = enKey;
    }
    // The transactional tree service additionally verifies ancestry and activity membership.
    public void SetParent(Guid? parentId)
    {
        if (parentId == Guid.Empty || parentId == Id) throw new ArgumentException("A category cannot parent itself or reference an empty id.", nameof(parentId));
        ParentId = parentId;
    }
    public void SetActive(bool active) => IsActive = active;
}
