using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Serialization.Converters;

/// <summary>Converts <see cref="TimeSpan"/> values to and from ISO 8601 duration strings.</summary>
public sealed class IsoDurationJsonConverter : JsonConverter<TimeSpan>
{
    /// <inheritdoc />
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("An ISO 8601 duration must be a JSON string.");
        }

        var value = reader.GetString();
        try
        {
            return XmlConvert.ToTimeSpan(value!);
        }
        catch (FormatException exception)
        {
            throw new JsonException("The value is not a valid ISO 8601 duration.", exception);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        => writer.WriteStringValue(XmlConvert.ToString(value));
}
