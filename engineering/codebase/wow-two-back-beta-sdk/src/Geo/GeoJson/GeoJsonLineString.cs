namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>A GeoJSON <c>LineString</c> — an ordered list of two or more positions.</summary>
public sealed record GeoJsonLineString : GeoJsonGeometry
{
    /// <summary>The line's positions in order.</summary>
    public required IReadOnlyList<GeoPosition> Positions { get; init; }

    /// <inheritdoc />
    public override string Type => "LineString";
}
