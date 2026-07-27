namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>Base type for the supported GeoJSON geometry objects (RFC 7946): Point, LineString, Polygon.</summary>
public abstract record GeoJsonGeometry
{
    /// <summary>Gets the GeoJSON <c>type</c> discriminator (e.g. <c>"Point"</c>).</summary>
    public abstract string Type { get; }
}

/// <summary>A GeoJSON <c>Point</c> — a single position.</summary>
/// <param name="Position">The point's position.</param>
public sealed record GeoJsonPoint(GeoPosition Position) : GeoJsonGeometry
{
    /// <inheritdoc />
    public override string Type => "Point";
}

/// <summary>A GeoJSON <c>LineString</c> — an ordered list of two or more positions.</summary>
/// <param name="Positions">The line's positions in order.</param>
public sealed record GeoJsonLineString(IReadOnlyList<GeoPosition> Positions) : GeoJsonGeometry
{
    /// <inheritdoc />
    public override string Type => "LineString";
}

/// <summary>
/// A GeoJSON <c>Polygon</c> — one or more linear rings. The first ring is the exterior boundary; any
/// further rings are interior holes. Each ring is a closed list of positions (first == last).
/// </summary>
/// <param name="Rings">The polygon's rings; the first is the exterior boundary.</param>
public sealed record GeoJsonPolygon(IReadOnlyList<IReadOnlyList<GeoPosition>> Rings) : GeoJsonGeometry
{
    /// <inheritdoc />
    public override string Type => "Polygon";
}
