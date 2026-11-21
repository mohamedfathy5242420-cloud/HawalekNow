namespace HawalekNow.Domain.Modules.Locations;

public static class LocationNames
{
    public static string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 160 || value.Any(char.IsControl)) throw new ArgumentException("A bilingual place name must contain 1 to 160 visible characters.", nameof(value));
        return value.Trim();
    }
}
public sealed class Governorate
{
    public Guid Id { get; private set; }
    public string NameAr { get; private set; } = "";
    public string NameEn { get; private set; } = "";
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private Governorate() { }
    public static Governorate Create(string ar, string en, bool active = true)
    {
        var place = new Governorate { Id = Guid.CreateVersion7() }; place.Edit(ar, en, active); return place;
    }
    public void Edit(string ar, string en, bool active)
    {
        var arabic = LocationNames.Validate(ar); var english = LocationNames.Validate(en);
        NameAr = arabic; NameEn = english; IsActive = active;
    }
}
public sealed class City
{
    public Guid Id { get; private set; }
    public Guid GovernorateId { get; private set; }
    public string NameAr { get; private set; } = "";
    public string NameEn { get; private set; } = "";
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private City() { }
    public static City Create(Guid governorateId, string ar, string en, bool active = true)
    {
        var place = new City { Id = Guid.CreateVersion7() }; place.Edit(governorateId, ar, en, active); return place;
    }
    public void Edit(Guid governorateId, string ar, string en, bool active)
    {
        if (governorateId == Guid.Empty) throw new ArgumentException("A governorate is required.", nameof(governorateId));
        var arabic = LocationNames.Validate(ar); var english = LocationNames.Validate(en);
        GovernorateId = governorateId; NameAr = arabic; NameEn = english; IsActive = active;
    }
}
public sealed class Area
{
    public Guid Id { get; private set; }
    public Guid GovernorateId { get; private set; }
    public Guid CityId { get; private set; }
    public string NameAr { get; private set; } = "";
    public string NameEn { get; private set; } = "";
    public bool IsActive { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private Area() { }
    public static Area Create(Guid governorateId, Guid cityId, string ar, string en, bool active = true)
    {
        var place = new Area { Id = Guid.CreateVersion7() }; place.Edit(governorateId, cityId, ar, en, active); return place;
    }
    public void Edit(Guid governorateId, Guid cityId, string ar, string en, bool active)
    {
        if (governorateId == Guid.Empty || cityId == Guid.Empty) throw new ArgumentException("Both administrative parents are required.");
        var arabic = LocationNames.Validate(ar); var english = LocationNames.Validate(en);
        GovernorateId = governorateId; CityId = cityId; NameAr = arabic; NameEn = english; IsActive = active;
    }
}
