using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json;

/// <summary>Value converter that serializes a CLR type to JSON for storage and deserializes on read — use for storing complex objects as Postgres <c>jsonb</c> or SqlServer <c>nvarchar(max)</c>.</summary>
/// <typeparam name="T">The CLR type being converted.</typeparam>
public sealed class JsonValueConverter<T> : ValueConverter<T, string>
{
    /// <summary>Initializes the converter with the stored preset, <see cref="StoredJsonConstants.Default"/>.</summary>
    public JsonValueConverter() : this(StoredJsonConstants.Default)
    {
    }

    /// <summary>Initializes the converter with a custom <see cref="JsonSerializerOptions"/>.</summary>
    /// <param name="options">The serializer options used for serialization and deserialization.</param>
    public JsonValueConverter(JsonSerializerOptions options)
        : base(
            v => JsonSerializer.Serialize(v, options),
            v => Deserialize(v, options))
    {
    }

    private static T Deserialize(string json, JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<T>(json, options);

        // A column holding the JSON literal `null` decodes without error and yields nothing to materialize.
        if (value is null)
        {
            throw new InvalidOperationException($"A JSON-mapped column decoded to null for '{typeof(T).Name}'.");
        }

        return value;
    }
}
