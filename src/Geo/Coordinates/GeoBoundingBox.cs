namespace WoW.Two.Sdk.Backend.Beta.Geo.Coordinates;

/// <summary>
/// An axis-aligned latitude/longitude rectangle (a GeoJSON-style bbox). Does not span the antimeridian —
/// <see cref="West"/> ≤ <see cref="East"/> and <see cref="South"/> ≤ <see cref="North"/>. Immutable.
/// </summary>
public sealed record GeoBoundingBox
{
    /// <summary>Creates a validated bounding box.</summary>
    /// <param name="south">Minimum latitude (southern edge).</param>
    /// <param name="west">Minimum longitude (western edge).</param>
    /// <param name="north">Maximum latitude (northern edge).</param>
    /// <param name="east">Maximum longitude (eastern edge).</param>
    /// <exception cref="ArgumentException">An edge ordering is inverted.</exception>
    public GeoBoundingBox(double south, double west, double north, double east)
    {
        if (south > north) throw new ArgumentException("South edge must not exceed north edge.", nameof(south));
        if (west > east) throw new ArgumentException("West edge must not exceed east edge.", nameof(west));

        South = south;
        West = west;
        North = north;
        East = east;
    }

    /// <summary>Gets the minimum latitude (southern edge).</summary>
    public double South { get; }

    /// <summary>Gets the minimum longitude (western edge).</summary>
    public double West { get; }

    /// <summary>Gets the maximum latitude (northern edge).</summary>
    public double North { get; }

    /// <summary>Gets the maximum longitude (eastern edge).</summary>
    public double East { get; }

    /// <summary>Gets the geometric centre of the box.</summary>
    public GeoCoordinate Center => new((South + North) / 2.0, (West + East) / 2.0);

    /// <summary>Gets the north-west corner.</summary>
    public GeoCoordinate NorthWest => new(North, West);

    /// <summary>Gets the south-east corner.</summary>
    public GeoCoordinate SouthEast => new(South, East);

    /// <summary>Returns whether the box contains the given coordinate (edges inclusive).</summary>
    /// <param name="coordinate">The coordinate to test.</param>
    /// <returns><see langword="true"/> when the coordinate lies within or on the box.</returns>
    public bool Contains(GeoCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return coordinate.Latitude >= South && coordinate.Latitude <= North
            && coordinate.Longitude >= West && coordinate.Longitude <= East;
    }

    /// <summary>Returns the smallest box containing both this box and <paramref name="coordinate"/>.</summary>
    /// <param name="coordinate">The coordinate to include.</param>
    /// <returns>A box grown to include the coordinate.</returns>
    public GeoBoundingBox Extend(GeoCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new GeoBoundingBox(
            Math.Min(South, coordinate.Latitude),
            Math.Min(West, coordinate.Longitude),
            Math.Max(North, coordinate.Latitude),
            Math.Max(East, coordinate.Longitude));
    }

    /// <summary>Builds the tightest bounding box enclosing all of <paramref name="coordinates"/>.</summary>
    /// <param name="coordinates">The coordinates to enclose (must be non-empty).</param>
    /// <returns>The enclosing box.</returns>
    /// <exception cref="ArgumentException">The sequence is empty.</exception>
    public static GeoBoundingBox FromCoordinates(IEnumerable<GeoCoordinate> coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);

        double south = double.MaxValue, west = double.MaxValue, north = double.MinValue, east = double.MinValue;
        var any = false;
        foreach (var c in coordinates)
        {
            south = Math.Min(south, c.Latitude);
            west = Math.Min(west, c.Longitude);
            north = Math.Max(north, c.Latitude);
            east = Math.Max(east, c.Longitude);
            any = true;
        }

        if (!any) throw new ArgumentException("At least one coordinate is required.", nameof(coordinates));
        return new GeoBoundingBox(south, west, north, east);
    }
}
