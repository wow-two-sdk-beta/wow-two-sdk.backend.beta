namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>A GeoJSON <c>LineString</c> — an ordered list of two or more positions.</summary>
/// <param name="Positions">The line's positions in order.</param>
public sealed record GeoJsonLineString(IReadOnlyList<GeoPosition> Positions) : GeoJsonGeometry
{
    /// <inheritdoc />
    public override string Type => "LineString";
}
