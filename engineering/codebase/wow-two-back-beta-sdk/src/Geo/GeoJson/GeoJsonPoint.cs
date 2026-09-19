namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>A GeoJSON <c>Point</c> — a single position.</summary>
public sealed record GeoJsonPoint : GeoJsonGeometry
{
    /// <summary>The point's position.</summary>
    public required GeoPosition Position { get; init; }

    /// <inheritdoc />
    public override string Type => "Point";
}
