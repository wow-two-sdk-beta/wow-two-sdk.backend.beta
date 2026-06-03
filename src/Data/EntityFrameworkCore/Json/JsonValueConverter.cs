using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Json;

/// <summary>
/// Value converter that serializes a CLR type to JSON for storage and deserializes on read.
/// Use for storing complex objects as Postgres <c>jsonb</c> or SqlServer <c>nvarchar(max)</c>.
/// </summary>
/// <typeparam name="T">The CLR type being converted.</typeparam>
public sealed class JsonValueConverter<T> : ValueConverter<T, string>
{
    /// <summary>Initializes the converter with default <see cref="JsonSerializerOptions"/>.</summary>
    public JsonValueConverter() : this(new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
    }

    /// <summary>Initializes the converter with a custom <see cref="JsonSerializerOptions"/>.</summary>
    public JsonValueConverter(JsonSerializerOptions options)
        : base(
            v => JsonSerializer.Serialize(v, options),
            v => JsonSerializer.Deserialize<T>(v, options)!)
    {
    }
}
