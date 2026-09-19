using System.Text;
using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson.Serializers;

/// <summary>
/// Defines serialization and decoding of supported GeoJSON objects (RFC 7946) — Point / LineString / Polygon geometries,
/// Features, and FeatureCollections — with the correct <c>[longitude, latitude(, altitude)]</c> position
/// ordering. Feature <c>properties</c> round-trip as raw <see cref="JsonElement"/> values. Hand-rolled over
/// <see cref="Utf8JsonWriter"/>/<see cref="JsonDocument"/>; no external dependency.
/// </summary>
/// <remarks>
/// Supports only Point, LineString and Polygon geometry with two- or three-element positions. Parsing requires the
/// matching root discriminator and required Feature/FeatureCollection members. Feature ids preserve their JSON string
/// or number kind. Bounding boxes and foreign members are discarded because the SDK model does not represent them;
/// geometry cardinality, polygon closure and ring winding remain caller validation.
/// </remarks>
public interface IGeoJsonSerializer
{
    /// <summary>Serializes <paramref name="geometry"/>.</summary>
    /// <param name="geometry">The value to serialize.</param>
    string Serialize(GeoJsonGeometry geometry);

    /// <summary>Serializes <paramref name="feature"/>.</summary>
    /// <param name="feature">The value to serialize.</param>
    string Serialize(GeoJsonFeature feature);

    /// <summary>Serializes <paramref name="collection"/>.</summary>
    /// <param name="collection">The value to serialize.</param>
    string Serialize(GeoJsonFeatureCollection collection);

    /// <summary>Parses <paramref name="json"/>.</summary>
    /// <param name="json">The value to parse.</param>
    GeoJsonGeometry ParseGeometry(string json);

    /// <summary>Parses <paramref name="json"/>.</summary>
    /// <param name="json">The value to parse.</param>
    GeoJsonFeature ParseFeature(string json);

    /// <summary>Parses <paramref name="json"/>.</summary>
    /// <param name="json">The value to parse.</param>
    GeoJsonFeatureCollection ParseFeatureCollection(string json);
}
