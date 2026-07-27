using System.Text;
using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Geo.GeoJson;

/// <summary>
/// Reads and writes the supported GeoJSON objects (RFC 7946) — Point / LineString / Polygon geometries,
/// Features, and FeatureCollections — with the correct <c>[longitude, latitude(, altitude)]</c> position
/// ordering. Feature <c>properties</c> round-trip as raw <see cref="JsonElement"/> values. Hand-rolled over
/// <see cref="Utf8JsonWriter"/>/<see cref="JsonDocument"/>; no external dependency.
/// </summary>
public static class GeoJsonSerializer
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false };

    /// <summary>Serializes a geometry to a GeoJSON string.</summary>
    /// <param name="geometry">The geometry to serialize.</param>
    /// <returns>The GeoJSON text.</returns>
    public static string Serialize(GeoJsonGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return Write(writer => WriteGeometry(writer, geometry));
    }

    /// <summary>Serializes a feature to a GeoJSON string.</summary>
    /// <param name="feature">The feature to serialize.</param>
    /// <returns>The GeoJSON text.</returns>
    public static string Serialize(GeoJsonFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);
        return Write(writer => WriteFeature(writer, feature));
    }

    /// <summary>Serializes a feature collection to a GeoJSON string.</summary>
    /// <param name="collection">The collection to serialize.</param>
    /// <returns>The GeoJSON text.</returns>
    public static string Serialize(GeoJsonFeatureCollection collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "FeatureCollection");
            writer.WritePropertyName("features");
            writer.WriteStartArray();
            foreach (var feature in collection.Features) WriteFeature(writer, feature);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    /// <summary>Parses a GeoJSON geometry object.</summary>
    /// <param name="json">The GeoJSON geometry text.</param>
    /// <returns>The parsed geometry.</returns>
    /// <exception cref="NotSupportedException">The geometry <c>type</c> is not one of Point/LineString/Polygon.</exception>
    public static GeoJsonGeometry ParseGeometry(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        return ReadGeometry(document.RootElement);
    }

    /// <summary>Parses a GeoJSON Feature.</summary>
    /// <param name="json">The GeoJSON feature text.</param>
    /// <returns>The parsed feature.</returns>
    public static GeoJsonFeature ParseFeature(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        return ReadFeature(document.RootElement);
    }

    /// <summary>Parses a GeoJSON FeatureCollection.</summary>
    /// <param name="json">The GeoJSON feature-collection text.</param>
    /// <returns>The parsed collection (empty when there are no features).</returns>
    public static GeoJsonFeatureCollection ParseFeatureCollection(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);

        var features = new List<GeoJsonFeature>();
        if (document.RootElement.TryGetProperty("features", out var array) && array.ValueKind == JsonValueKind.Array)
            foreach (var feature in array.EnumerateArray())
                features.Add(ReadFeature(feature));

        return new GeoJsonFeatureCollection(features);
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
            body(writer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteGeometry(Utf8JsonWriter writer, GeoJsonGeometry geometry)
    {
        writer.WriteStartObject();
        writer.WriteString("type", geometry.Type);
        writer.WritePropertyName("coordinates");

        switch (geometry)
        {
            case GeoJsonPoint point:
                WritePosition(writer, point.Position);
                break;
            case GeoJsonLineString line:
                WritePositions(writer, line.Positions);
                break;
            case GeoJsonPolygon polygon:
                writer.WriteStartArray();
                foreach (var ring in polygon.Rings) WritePositions(writer, ring);
                writer.WriteEndArray();
                break;
            default:
                throw new NotSupportedException($"Unsupported geometry type '{geometry.Type}'.");
        }

        writer.WriteEndObject();
    }

    private static void WriteFeature(Utf8JsonWriter writer, GeoJsonFeature feature)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");

        if (feature.Id is { } id)
            writer.WriteString("id", id);

        writer.WritePropertyName("geometry");
        if (feature.Geometry is { } geometry) WriteGeometry(writer, geometry);
        else writer.WriteNullValue();

        writer.WritePropertyName("properties");
        if (feature.Properties is { } properties)
        {
            writer.WriteStartObject();
            foreach (var pair in properties)
            {
                writer.WritePropertyName(pair.Key);
                pair.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNullValue();
        }

        writer.WriteEndObject();
    }

    private static void WritePosition(Utf8JsonWriter writer, GeoPosition position)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(position.Longitude);
        writer.WriteNumberValue(position.Latitude);
        if (position.AltitudeMeters is { } altitude) writer.WriteNumberValue(altitude);
        writer.WriteEndArray();
    }

    private static void WritePositions(Utf8JsonWriter writer, IReadOnlyList<GeoPosition> positions)
    {
        writer.WriteStartArray();
        foreach (var position in positions) WritePosition(writer, position);
        writer.WriteEndArray();
    }

    private static GeoJsonGeometry ReadGeometry(JsonElement element)
    {
        var type = element.GetProperty("type").GetString();
        var coordinates = element.GetProperty("coordinates");

        return type switch
        {
            "Point" => new GeoJsonPoint(ReadPosition(coordinates)),
            "LineString" => new GeoJsonLineString(ReadPositions(coordinates)),
            "Polygon" => new GeoJsonPolygon(coordinates.EnumerateArray().Select(ReadPositions).ToArray()),
            _ => throw new NotSupportedException($"Unsupported GeoJSON geometry type '{type}'."),
        };
    }

    private static GeoJsonFeature ReadFeature(JsonElement element)
    {
        GeoJsonGeometry? geometry = null;
        if (element.TryGetProperty("geometry", out var geometryElement) && geometryElement.ValueKind == JsonValueKind.Object)
            geometry = ReadGeometry(geometryElement);

        IReadOnlyDictionary<string, JsonElement>? properties = null;
        if (element.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
        {
            var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var property in propertiesElement.EnumerateObject())
                map[property.Name] = property.Value.Clone();
            properties = map;
        }

        string? id = null;
        if (element.TryGetProperty("id", out var idElement))
            id = idElement.ValueKind == JsonValueKind.String ? idElement.GetString() : idElement.GetRawText();

        return new GeoJsonFeature(geometry, properties, id);
    }

    private static GeoPosition ReadPosition(JsonElement element)
    {
        var longitude = element[0].GetDouble();
        var latitude = element[1].GetDouble();
        double? altitude = element.GetArrayLength() > 2 ? element[2].GetDouble() : null;
        return new GeoPosition(longitude, latitude, altitude);
    }

    private static IReadOnlyList<GeoPosition> ReadPositions(JsonElement element)
        => element.EnumerateArray().Select(ReadPosition).ToArray();
}
