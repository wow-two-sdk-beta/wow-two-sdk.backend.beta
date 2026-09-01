using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>A GeoJSON <c>FeatureCollection</c> — an ordered set of features.</summary>
/// <param name="Features">The features in the collection.</param>
public sealed record GeoJsonFeatureCollection(IReadOnlyList<GeoJsonFeature> Features);
