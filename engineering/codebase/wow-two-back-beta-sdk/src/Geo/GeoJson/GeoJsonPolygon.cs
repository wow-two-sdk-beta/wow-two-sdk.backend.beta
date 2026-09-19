namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>
/// A GeoJSON <c>Polygon</c> — one or more linear rings. The first ring is the exterior boundary; any
/// further rings are interior holes. Each ring is a closed list of positions (first == last).
/// </summary>
public sealed record GeoJsonPolygon : GeoJsonGeometry
{
    /// <summary>The polygon's rings; the first is the exterior boundary.</summary>
    public required IReadOnlyList<IReadOnlyList<GeoPosition>> Rings { get; init; }

    /// <inheritdoc />
    public override string Type => "Polygon";
}
