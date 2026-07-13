using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>
/// A GeoJSON <c>Feature</c> — a geometry with an optional id and a free-form <c>properties</c> bag.
/// Properties are kept as raw <see cref="JsonElement"/> values so arbitrary application metadata round-trips.
/// </summary>
/// <param name="Geometry">The feature's geometry, or <see langword="null"/> for a geometry-less feature.</param>
/// <param name="Properties">Arbitrary properties keyed by name, or <see langword="null"/>.</param>
/// <param name="Id">An optional feature identifier.</param>
public sealed record GeoJsonFeature(
    GeoJsonGeometry? Geometry,
    IReadOnlyDictionary<string, JsonElement>? Properties = null,
    string? Id = null);

/// <summary>A GeoJSON <c>FeatureCollection</c> — an ordered set of features.</summary>
/// <param name="Features">The features in the collection.</param>
public sealed record GeoJsonFeatureCollection(IReadOnlyList<GeoJsonFeature> Features);
