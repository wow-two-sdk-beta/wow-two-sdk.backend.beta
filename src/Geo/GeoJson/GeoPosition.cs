using WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>
/// A GeoJSON position — longitude, latitude, and optional altitude, in that order (RFC 7946 uses
/// <c>[longitude, latitude]</c>, the reverse of the usual spoken order). Convert to/from the
/// latitude-first <see cref="GeoCoordinate"/> with <see cref="FromCoordinate"/> / <see cref="ToCoordinate"/>.
/// </summary>
/// <param name="Longitude">Longitude in decimal degrees.</param>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="AltitudeMeters">Optional altitude in metres.</param>
public readonly record struct GeoPosition(double Longitude, double Latitude, double? AltitudeMeters = null)
{
    /// <summary>Creates a position from a latitude-first <see cref="GeoCoordinate"/>.</summary>
    /// <param name="coordinate">The coordinate to convert.</param>
    /// <returns>The equivalent GeoJSON position.</returns>
    public static GeoPosition FromCoordinate(GeoCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new GeoPosition(coordinate.Longitude, coordinate.Latitude, coordinate.AltitudeMeters);
    }

    /// <summary>Converts this position to a latitude-first <see cref="GeoCoordinate"/>.</summary>
    /// <returns>The equivalent coordinate.</returns>
    public GeoCoordinate ToCoordinate() => new(Latitude, Longitude, AltitudeMeters);
}
