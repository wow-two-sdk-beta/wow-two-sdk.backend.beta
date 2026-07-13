using WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

namespace WoW.Two.Sdk.Backend.Beta.Geo.Distance;

/// <summary>
/// Default <see cref="IGeoDistanceCalculator"/> using the haversine formula on a spherical Earth (IUGG
/// mean radius 6 371 008.8 m). Accurate to well under 1% for terrestrial distances; stateless and thread-safe.
/// For sub-metre precision over long baselines use an ellipsoidal (Vincenty) model instead.
/// </summary>
public sealed class GeoDistanceCalculator : IGeoDistanceCalculator
{
    /// <summary>The IUGG mean Earth radius in metres.</summary>
    public const double EarthRadiusMeters = 6_371_008.8;

    /// <inheritdoc />
    public double DistanceMeters(GeoCoordinate from, GeoCoordinate to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var dLat = to.LatitudeRadians - from.LatitudeRadians;
        var dLon = to.LongitudeRadians - from.LongitudeRadians;

        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(from.LatitudeRadians) * Math.Cos(to.LatitudeRadians) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));

        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
    }

    /// <inheritdoc />
    public double Distance(GeoCoordinate from, GeoCoordinate to, DistanceUnit unit)
        => DistanceUnitExtensions.FromMeters(DistanceMeters(from, to), unit);

    /// <inheritdoc />
    public double InitialBearingDegrees(GeoCoordinate from, GeoCoordinate to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var dLon = to.LongitudeRadians - from.LongitudeRadians;

        var y = Math.Sin(dLon) * Math.Cos(to.LatitudeRadians);
        var x = (Math.Cos(from.LatitudeRadians) * Math.Sin(to.LatitudeRadians))
            - (Math.Sin(from.LatitudeRadians) * Math.Cos(to.LatitudeRadians) * Math.Cos(dLon));

        var degrees = Math.Atan2(y, x) * 180.0 / Math.PI;
        return (degrees + 360.0) % 360.0;
    }

    /// <inheritdoc />
    public GeoCoordinate Destination(GeoCoordinate from, double bearingDegrees, double distanceMeters)
    {
        ArgumentNullException.ThrowIfNull(from);

        var angular = distanceMeters / EarthRadiusMeters;
        var bearing = bearingDegrees * Math.PI / 180.0;
        var lat1 = from.LatitudeRadians;
        var lon1 = from.LongitudeRadians;

        var lat2 = Math.Asin((Math.Sin(lat1) * Math.Cos(angular))
            + (Math.Cos(lat1) * Math.Sin(angular) * Math.Cos(bearing)));

        var lon2 = lon1 + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(lat1),
            Math.Cos(angular) - (Math.Sin(lat1) * Math.Sin(lat2)));

        var latDegrees = lat2 * 180.0 / Math.PI;
        var lonDegrees = (((lon2 * 180.0 / Math.PI) + 540.0) % 360.0) - 180.0;

        return new GeoCoordinate(latDegrees, lonDegrees);
    }
}
