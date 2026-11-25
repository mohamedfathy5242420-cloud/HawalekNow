using HawalekNow.Domain.Modules.Locations;

namespace HawalekNow.UnitTests;

public sealed class LocationModelTests
{
    [Fact]
    public void Names_and_both_area_parents_are_required_without_partial_edits()
    {
        Assert.Throws<ArgumentException>(() => Governorate.Create("", "Name"));
        Assert.Throws<ArgumentException>(() => City.Create(Guid.Empty, "اسم", "Name"));
        Assert.Throws<ArgumentException>(() => Area.Create(Guid.NewGuid(), Guid.Empty, "اسم", "Name"));
        var area = Area.Create(Guid.NewGuid(), Guid.NewGuid(), "اسم", "Name"); var governorate = area.GovernorateId; var city = area.CityId;
        Assert.Throws<ArgumentException>(() => area.Edit(Guid.NewGuid(), Guid.NewGuid(), "اسم", "", false));
        Assert.Equal(governorate, area.GovernorateId); Assert.Equal(city, area.CityId); Assert.True(area.IsActive); Assert.Equal("Name", area.NameEn);
    }
    [Fact]
    public void Distinct_source_districts_can_share_an_English_display_name()
    {
        var governorate = Governorate.Create("الدقهلية", "Dakahlia");
        var district = City.Create(governorate.Id, "قسم ميت غمر", "Mit Ghamr"); var center = City.Create(governorate.Id, "مركز ميت غمر", "Mit Ghamr");
        Assert.NotEqual(district.Id, center.Id); Assert.Equal(district.NameEn, center.NameEn);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Administrative_name_storage_limits_are_enforced(bool arabic)
    {
        var ar = arabic ? new string('ع', 160) : "اسم"; var en = arabic ? "Name" : new string('N', 160);
        Assert.Equal(ar, Governorate.Create(ar, en).NameAr);
        Assert.Throws<ArgumentException>(() => Governorate.Create(arabic ? ar + "ع" : ar, arabic ? en : en + "N"));
    }
}
