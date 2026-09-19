using WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>
/// A GeoJSON position — longitude, latitude, and optional altitude, in that order (RFC 7946 uses
/// <c>[longitude, latitude]</c>, the reverse of the usual spoken order). Convert to/from the
/// latitude-first <see cref="GeoCoordinate"/> with <see cref="FromCoordinate"/> / <see cref="ToCoordinate"/>.
/// </summary>
public readonly record struct GeoPosition
{
    /// <summary>Longitude in decimal degrees.</summary>
    public required double Longitude { get; init; }

    /// <summary>Latitude in decimal degrees.</summary>
    public required double Latitude { get; init; }

    /// <summary>Optional altitude in metres.</summary>
    public double? AltitudeMeters { get; init; }

    /// <summary>Creates a position from a latitude-first <see cref="GeoCoordinate"/>.</summary>
    /// <param name="coordinate">The coordinate to convert.</param>
    /// <returns>The equivalent GeoJSON position.</returns>
    public static GeoPosition FromCoordinate(GeoCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new GeoPosition { Longitude = coordinate.Longitude, Latitude = coordinate.Latitude, AltitudeMeters = coordinate.AltitudeMeters };
    }

    /// <summary>Converts this position to a latitude-first <see cref="GeoCoordinate"/>.</summary>
    /// <returns>The equivalent coordinate.</returns>
    public GeoCoordinate ToCoordinate() => new(Latitude, Longitude, AltitudeMeters);
}
