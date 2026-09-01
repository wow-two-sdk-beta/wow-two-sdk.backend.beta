namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>Base type for the supported GeoJSON geometry objects (RFC 7946): Point, LineString, Polygon.</summary>
public abstract record GeoJsonGeometry
{
    /// <summary>Gets the GeoJSON <c>type</c> discriminator (e.g. <c>"Point"</c>).</summary>
    public abstract string Type { get; }
}
