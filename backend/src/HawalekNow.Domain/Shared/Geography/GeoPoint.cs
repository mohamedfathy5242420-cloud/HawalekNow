namespace HawalekNow.Domain.Shared.Geography;

public readonly record struct GeoPoint
{
    public double Latitude { get; }
    public double Longitude { get; }
    private GeoPoint(double latitude, double longitude) { Latitude = latitude; Longitude = longitude; }
    public static GeoPoint Create(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude) || latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (!double.IsFinite(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        return new(latitude, longitude);
    }
}
