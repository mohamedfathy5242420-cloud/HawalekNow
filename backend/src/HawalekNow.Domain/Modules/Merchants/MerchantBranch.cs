using HawalekNow.Domain.Shared.Geography;

namespace HawalekNow.Domain.Modules.Merchants;

public enum MerchantPlaceKind { Branch = 1, PickupPoint = 2 }

public sealed class MerchantBranch
{
    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public MerchantPlaceKind Kind { get; private set; }
    public string Name { get; private set; } = "";
    public string Address { get; private set; } = "";
    public Guid GovernorateId { get; private set; }
    public Guid CityId { get; private set; }
    public Guid AreaId { get; private set; }
    public GeoPoint Position { get; private set; }
    public string? Phone { get; private set; }
    public string? WhatsApp { get; private set; }
    public bool PublicFixedAddressConfirmed { get; private set; }
    public WeeklyHours Hours { get; private set; } = WeeklyHours.AllClosed;
    public byte[] RowVersion { get; private set; } = [];
    private MerchantBranch() { }
    public static MerchantBranch Create(Guid storeId, BranchDetails details, WeeklyHours hours)
    {
        if (storeId == Guid.Empty) throw new ArgumentException("A store is required.");
        var branch = new MerchantBranch { Id = Guid.CreateVersion7(), StoreId = storeId }; branch.Update(details); branch.SetHours(hours); return branch;
    }
    public void SetHours(WeeklyHours hours) { ArgumentNullException.ThrowIfNull(hours); Hours = hours; }
    public void Update(BranchDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Kind = details.Kind; Name = details.Name; Address = details.Address; GovernorateId = details.GovernorateId; CityId = details.CityId;
        AreaId = details.AreaId; Position = details.Position; Phone = details.Phone; WhatsApp = details.WhatsApp; PublicFixedAddressConfirmed = true;
    }
}

public sealed record BranchDetails
{
    public MerchantPlaceKind Kind { get; }
    public string Name { get; }
    public string Address { get; }
    public Guid GovernorateId { get; }
    public Guid CityId { get; }
    public Guid AreaId { get; }
    public GeoPoint Position { get; }
    public string? Phone { get; }
    public string? WhatsApp { get; }
    private BranchDetails(MerchantPlaceKind kind, string name, string address, Guid governorateId, Guid cityId, Guid areaId, GeoPoint position, string? phone, string? whatsApp) =>
        (Kind, Name, Address, GovernorateId, CityId, AreaId, Position, Phone, WhatsApp) = (kind, name, address, governorateId, cityId, areaId, position, phone, whatsApp);
    public static bool TryCreate(string? kind, string? name, string? address, Guid governorateId, Guid cityId, Guid areaId, double? latitude, double? longitude,
        string? phone, string? whatsApp, bool publicFixedAddressConfirmed, out BranchDetails? details)
    {
        details = null;
        if (!Enum.GetNames<MerchantPlaceKind>().Any(value => value.Equals(kind, StringComparison.OrdinalIgnoreCase)) || !publicFixedAddressConfirmed ||
            string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200 || name.Any(char.IsControl) || string.IsNullOrWhiteSpace(address) || address.Trim().Length > 600 || address.Any(char.IsControl) ||
            governorateId == Guid.Empty || cityId == Guid.Empty || areaId == Guid.Empty || latitude is null || longitude is null || !double.IsFinite(latitude.Value) || !double.IsFinite(longitude.Value) ||
            latitude is < -90 or > 90 || longitude is < -180 or > 180 || !StoreDetails.Contact(phone, out var normalizedPhone) || !StoreDetails.Contact(whatsApp, out var normalizedWhatsApp)) return false;
        details = new(Enum.Parse<MerchantPlaceKind>(kind!, true), name.Trim(), address.Trim(), governorateId, cityId, areaId, GeoPoint.Create(latitude.Value, longitude.Value), normalizedPhone, normalizedWhatsApp); return true;
    }
}
