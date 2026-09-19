using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>A GeoJSON <c>FeatureCollection</c> — an ordered set of features.</summary>
public sealed record GeoJsonFeatureCollection
{
    /// <summary>The features in the collection.</summary>
    public required IReadOnlyList<GeoJsonFeature> Features { get; init; }
}
