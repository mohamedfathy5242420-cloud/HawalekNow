using HawalekNow.Application.Modules.Merchants;
using HawalekNow.Domain.Modules.Merchants;
using HawalekNow.Infrastructure.Modules.Merchants;

namespace HawalekNow.UnitTests;

public sealed class BranchTests
{
    private static readonly Guid Governorate = Guid.NewGuid();
    private static readonly Guid City = Guid.NewGuid();
    private static readonly Guid Area = Guid.NewGuid();
    private static bool Details(string? kind, double? latitude, double? longitude, bool confirmed, string? phone, string? whatsApp, out BranchDetails? details) =>
        BranchDetails.TryCreate(kind, "نقطة عامة", "عنوان تجاري ثابت", Governorate, City, Area, latitude, longitude, phone, whatsApp, confirmed, out details);

    [Theory]
    [InlineData("Branch")]
    [InlineData("PickupPoint")]
    [InlineData("pickuppoint")]
    public void Named_place_kinds_have_a_single_name_and_opaque_administrative_ids(string kind)
    {
        Assert.True(Details(kind, 30.0444, 31.2357, true, null, null, out var details));
        var storeId = Guid.NewGuid(); var branch = MerchantBranch.Create(storeId, details!, WeeklyHours.AllClosed);
        Assert.Equal(storeId, branch.StoreId); Assert.Equal(30.0444, branch.Position.Latitude); Assert.Equal(31.2357, branch.Position.Longitude);
        Assert.Equal(Governorate, branch.GovernorateId); Assert.Equal(City, branch.CityId); Assert.Equal(Area, branch.AreaId); Assert.True(branch.PublicFixedAddressConfirmed);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_or_numeric_kind_names_are_rejected(string? kind) => Assert.False(Details(kind, 30, 31, true, null, null, out _));

    [Theory]
    [InlineData(null, 31d)]
    [InlineData(30d, null)]
    [InlineData(91d, 31d)]
    [InlineData(-91d, 31d)]
    [InlineData(30d, 181d)]
    [InlineData(30d, -181d)]
    [InlineData(double.NaN, 31d)]
    [InlineData(30d, double.PositiveInfinity)]
    public void Coordinates_must_be_explicit_finite_and_within_geographic_bounds(double? latitude, double? longitude) => Assert.False(Details("Branch", latitude, longitude, true, null, null, out _));

    [Fact]
    public void The_public_fixed_address_confirmation_and_contact_validity_are_required()
    {
        Assert.False(Details("PickupPoint", 30, 31, false, null, null, out _));
        Assert.False(Details("Branch", 30, 31, true, "not-a-phone", null, out _));
        Assert.False(BranchDetails.TryCreate("Branch", "name", " ", Governorate, City, Area, 30, 31, null, null, true, out _));
        Assert.False(BranchDetails.TryCreate("Branch", "name", "address", Governorate, City, Guid.Empty, 30, 31, null, null, true, out _));
        var write = new BranchWrite("Branch", "name", "address", Governorate, City, Area, 30, 31, true, Hours: HoursInput.ToWrite(WeeklyHours.AllClosed));
        Assert.True(new CreateBranchValidator().Validate(new CreateBranchCommand(Guid.NewGuid(), Guid.NewGuid(), write)).IsValid);
        Assert.False(new CreateBranchValidator().Validate(new CreateBranchCommand(Guid.Empty, Guid.NewGuid(), write)).IsValid);
    }

    [Theory]
    [InlineData(null, null, "+201012345678", "+201112345678")]
    [InlineData("+201212345678", null, "+201212345678", "+201112345678")]
    [InlineData(null, "+201512345678", "+201012345678", "+201512345678")]
    public void Fallback_applies_independently_to_each_channel(string? phone, string? whatsApp, string expectedPhone, string expectedWhatsApp)
    {
        Assert.True(StoreDetails.TryCreate("store", "+201012345678", "+201112345678", null, null, null, out var storeDetails));
        var store = Store.Create(Guid.NewGuid(), Guid.NewGuid(), storeDetails!);
        Assert.True(Details("PickupPoint", 30, 31, true, phone, whatsApp, out var details));
        var branch = MerchantBranch.Create(store.Id, details!, WeeklyHours.AllClosed); var now = DateTimeOffset.Parse("2026-11-26T10:00:00Z"); var dto = BranchService.ToPublicDto(branch, store, now);
        Assert.Equal(expectedPhone, dto.Phone); Assert.Equal(expectedWhatsApp, dto.WhatsApp);
        Assert.Equal(phone, BranchService.ToDto(branch, store, now).Phone); Assert.Equal(whatsApp, BranchService.ToDto(branch, store, now).WhatsApp);
    }
}
