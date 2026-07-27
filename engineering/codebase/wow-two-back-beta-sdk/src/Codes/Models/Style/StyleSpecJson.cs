using System.Text.Json;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Provides camelCase JSON (de)serialization of <see cref="StyleSpec"/> for the <c>CodeEntity.StyleJson</c> column, falling back to <see cref="StyleSpec.Default"/> on null, blank, or malformed input so an unstyled code still renders.</summary>
public static class StyleSpecJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes a spec to camelCase JSON for storage.</summary>
    public static string Serialize(StyleSpec spec) => JsonSerializer.Serialize(spec, Options);

    /// <summary>Deserializes stored JSON to a spec, returning <see cref="StyleSpec.Default"/> for null, blank, or malformed input.</summary>
    /// <param name="json">The persisted <c>StyleJson</c> payload, or null when no style was saved.</param>
    public static StyleSpec Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return StyleSpec.Default;

        try
        {
            var spec = JsonSerializer.Deserialize<StyleSpec>(json, Options);
            return spec ?? StyleSpec.Default;
        }
        catch (JsonException)
        {
            // Fall back to the default so an unreadable style descriptor still renders rather than failing the image request.
            return StyleSpec.Default;
        }
    }
}
