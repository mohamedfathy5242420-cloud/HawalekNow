using HawalekNow.Domain.Shared.Geography;

namespace HawalekNow.UnitTests;

public sealed class GeoPointTests
{
    [Theory]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(30.0444, 31.2357)]
    public void Valid_inclusive_bounds_preserve_latitude_and_longitude(double latitude, double longitude)
    { var point = GeoPoint.Create(latitude, longitude); Assert.Equal(latitude, point.Latitude); Assert.Equal(longitude, point.Longitude); }
    [Theory]
    [InlineData(-90.0001, 0)]
    [InlineData(90.0001, 0)]
    [InlineData(0, -180.0001)]
    [InlineData(0, 180.0001)]
    [InlineData(double.NaN, 0)]
    [InlineData(0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(double.NegativeInfinity, 0)]
    [InlineData(0, double.PositiveInfinity)]
    [InlineData(0, double.NegativeInfinity)]
    public void Invalid_coordinates_are_rejected(double latitude, double longitude) => Assert.Throws<ArgumentOutOfRangeException>(() => GeoPoint.Create(latitude, longitude));
}
