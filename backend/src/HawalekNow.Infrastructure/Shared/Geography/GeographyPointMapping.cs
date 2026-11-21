using HawalekNow.Domain.Shared.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

namespace HawalekNow.Infrastructure.Shared.Geography;

public static class GeographyPointMapping
{
    public const int Srid = 4326;
    public static Point ToSqlPoint(GeoPoint value) => new(value.Longitude, value.Latitude) { SRID = Srid };
    public static GeoPoint FromSqlPoint(Point value)
    {
        if (value is null || value.IsEmpty || value.Coordinate is null || value.SRID != Srid || !double.IsNaN(value.Z) || !double.IsNaN(value.M)) throw new ArgumentException("A two-dimensional SRID4326 point is required.");
        return GeoPoint.Create(value.Y, value.X);
    }
    // Future module-owned mappings opt in; this creates no entity or production table.
    public static PropertyBuilder<GeoPoint> HasGeographyPoint(this PropertyBuilder<GeoPoint> property) =>
        property.HasConversion(new ValueConverter<GeoPoint, Point>(value => ToSqlPoint(value), value => FromSqlPoint(value))).HasColumnType("geography");
}
