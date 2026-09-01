namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>A GeoJSON <c>Point</c> — a single position.</summary>
/// <param name="Position">The point's position.</param>
public sealed record GeoJsonPoint(GeoPosition Position) : GeoJsonGeometry
{
    /// <inheritdoc />
    public override string Type => "Point";
}
