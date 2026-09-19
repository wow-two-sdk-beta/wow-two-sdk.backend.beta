using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>
/// A GeoJSON <c>Feature</c> — a geometry with an optional id and a free-form <c>properties</c> bag.
/// Properties are kept as raw <see cref="JsonElement"/> values so arbitrary application metadata round-trips.
/// </summary>
public sealed record GeoJsonFeature
{
    /// <summary>The feature's geometry, or <see langword="null"/> for a geometry-less feature.</summary>
    public required GeoJsonGeometry? Geometry { get; init; }

    /// <summary>Arbitrary properties keyed by name, or <see langword="null"/>.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>An optional string or number identifier, preserved as its original JSON kind.</summary>
    public JsonElement? Id { get; init; }
}
