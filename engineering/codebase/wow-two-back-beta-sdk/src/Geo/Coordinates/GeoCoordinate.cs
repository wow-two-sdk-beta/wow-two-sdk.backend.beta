using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

/// <summary>
/// A WGS-84 geographic coordinate — latitude/longitude in decimal degrees, with an optional altitude in
/// metres. Validated on construction: latitude ∈ [-90, 90], longitude ∈ [-180, 180]. Immutable.
/// </summary>
public sealed record GeoCoordinate
{
    /// <summary>Creates a validated coordinate.</summary>
    /// <param name="latitude">Latitude in decimal degrees, −90 to 90.</param>
    /// <param name="longitude">Longitude in decimal degrees, −180 to 180.</param>
    /// <param name="altitudeMeters">Optional altitude above the ellipsoid, in metres.</param>
    /// <exception cref="ArgumentOutOfRangeException">A component is outside its valid range.</exception>
    public GeoCoordinate(double latitude, double longitude, double? altitudeMeters = null)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, 90.0, nameof(latitude));
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, -90.0, nameof(latitude));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, 180.0, nameof(longitude));
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, -180.0, nameof(longitude));

        Latitude = latitude;
        Longitude = longitude;
        AltitudeMeters = altitudeMeters;
    }

    /// <summary>Gets the latitude in decimal degrees.</summary>
    public double Latitude { get; }

    /// <summary>Gets the longitude in decimal degrees.</summary>
    public double Longitude { get; }

    /// <summary>Gets the optional altitude above the ellipsoid, in metres.</summary>
    public double? AltitudeMeters { get; }

    /// <summary>Gets the latitude in radians.</summary>
    public double LatitudeRadians => Latitude * Math.PI / 180.0;

    /// <summary>Gets the longitude in radians.</summary>
    public double LongitudeRadians => Longitude * Math.PI / 180.0;

    /// <summary>Returns the coordinate as an <c>"lat,lon"</c> invariant-culture string.</summary>
    /// <returns>The formatted coordinate.</returns>
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{Latitude},{Longitude}");
}
