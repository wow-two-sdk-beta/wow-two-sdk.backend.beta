using WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

namespace WoW.Two.Sdk.Backend.Beta.Geo.Distance;

/// <summary>Computes great-circle distances, bearings, and destination points on the Earth's surface.</summary>
public interface IGeoDistanceCalculator
{
    /// <summary>Returns the great-circle (surface) distance between two coordinates in metres.</summary>
    /// <param name="from">The first coordinate.</param>
    /// <param name="to">The second coordinate.</param>
    /// <returns>The distance in metres.</returns>
    double DistanceMeters(GeoCoordinate from, GeoCoordinate to);

    /// <summary>Returns the distance between two coordinates converted to the requested unit.</summary>
    /// <param name="from">The first coordinate.</param>
    /// <param name="to">The second coordinate.</param>
    /// <param name="unit">The unit to express the result in.</param>
    /// <returns>The distance in <paramref name="unit"/>.</returns>
    double Distance(GeoCoordinate from, GeoCoordinate to, DistanceUnit unit);

    /// <summary>Returns the initial bearing (forward azimuth) from one coordinate to another, in degrees clockwise from north (0–360).</summary>
    /// <param name="from">The start coordinate.</param>
    /// <param name="to">The destination coordinate.</param>
    /// <returns>The initial bearing in degrees.</returns>
    double InitialBearingDegrees(GeoCoordinate from, GeoCoordinate to);

    /// <summary>
    /// Returns the coordinate reached by travelling <paramref name="distanceMeters"/> along the given
    /// bearing from a start point (direct geodesic problem, spherical model).
    /// </summary>
    /// <param name="from">The start coordinate.</param>
    /// <param name="bearingDegrees">The bearing in degrees clockwise from north.</param>
    /// <param name="distanceMeters">The distance to travel in metres.</param>
    /// <returns>The destination coordinate.</returns>
    GeoCoordinate Destination(GeoCoordinate from, double bearingDegrees, double distanceMeters);
}
