using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

/// <summary>Serializes event bodies as CloudEvents 1.0 JSON documents and decodes their data payloads.</summary>
/// <remarks>
///   - emits attributes and <c>data</c> in one JSON body, so the event survives a broker that drops unknown headers
///   - <c>type</c> is <see cref="IMessageTypeMapper.ToTypeToken"/>, so <c>MapMessageType&lt;T&gt;("com.acme.orders.placed")</c> is what gives a contract a reverse-DNS type
///   - <c>source</c>+<c>id</c> dedup is keyed apart from the inbox's <c>wt-message-id</c> (seam notes in <c>serialization.md</c>)
/// </remarks>
public sealed class CloudEventsMessageSerializer : IMessageSerializer
{
    /// <summary>Holds the content type this serializer stamps — the CloudEvents JSON event format media type.</summary>
    public const string CloudEventsJsonContentType = "application/cloudevents+json";

    /// <summary>Holds the CloudEvents specification version this serializer emits.</summary>
    public const string SpecVersion = "1.0";

    private const string DataContentType = "application/json";

    private readonly IMessageTypeMapper _typeResolver;
    private readonly CloudEventsSerializerOptions _options;
    private readonly JsonSerializerOptions _json;

    /// <summary>Create the serializer.</summary>
    /// <param name="typeResolver">Supplies the CloudEvents <c>type</c> attribute from the body's CLR type.</param>
    /// <param name="options">Producer identity and optional context attributes; null uses the defaults.</param>
    public CloudEventsMessageSerializer(IMessageTypeMapper typeResolver, CloudEventsSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeResolver);
        _typeResolver = typeResolver;
        _options = options ?? new CloudEventsSerializerOptions();
        _json = _options.Json ?? JsonOptionsConstants.Default;
    }

    /// <inheritdoc />
    public string ContentType => CloudEventsJsonContentType;

    /// <inheritdoc />
    public byte[] Serialize(object body, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(bodyType);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("specversion"u8, SpecVersion);
            writer.WriteString("id"u8, _options.IdFactory?.Invoke(body, bodyType) ?? Guid.NewGuid().ToString("D"));
            writer.WriteString("source"u8, _options.Source.ToString());
            writer.WriteString("type"u8, _typeResolver.ToTypeToken(bodyType));
            writer.WriteString("time"u8, _options.Clock.GetUtcNow().UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

            if (!string.IsNullOrEmpty(_options.Subject))
                writer.WriteString("subject"u8, _options.Subject);

            if (_options.DataSchema is not null)
                writer.WriteString("dataschema"u8, _options.DataSchema.ToString());

            writer.WriteString("datacontenttype"u8, DataContentType);
            writer.WritePropertyName("data"u8);
            JsonSerializer.Serialize(writer, body, bodyType, _json);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <inheritdoc />
    /// <remarks>
    ///   - decodes the <c>data</c> payload only, dropping every inbound context attribute
    ///   - <c>data_base64</c> decodes to the raw bytes when <paramref name="bodyType"/> is <c>byte[]</c>, otherwise parses as JSON
    ///   - validates the complete JSON document before decoding; inbound context attributes are not semantically validated
    ///   - fails when the document is malformed or not an object, or when its data payload is absent, null, or undecodable
    /// </remarks>
    public Result<object> Deserialize(ReadOnlySpan<byte> data, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(bodyType);
        if (data.IsEmpty)
            return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                $"An empty payload cannot be decoded into '{bodyType.Name}'."));

        try
        {
            var reader = new Utf8JsonReader(data);
            using var document = JsonDocument.ParseValue(ref reader);
            if (reader.Read())
                return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                    "A CloudEvents structured payload must contain one JSON document."));

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                    "A CloudEvents structured payload must be a JSON object."));

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("data"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                        return EmptyData(bodyType);

                    return Decoded(property.Value.Deserialize(bodyType, _json), bodyType);
                }

                if (property.NameEquals("data_base64"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                        return EmptyData(bodyType);

                    if (property.Value.ValueKind != JsonValueKind.String)
                        return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                            "The CloudEvent data_base64 payload must be a base64 string."));

                    var decoded = property.Value.GetBytesFromBase64();
                    if (bodyType == typeof(byte[]))
                        return Result<object>.Ok(decoded);

                    return Decoded(JsonSerializer.Deserialize(decoded, bodyType, _json), bodyType);
                }
            }

            return EmptyData(bodyType); // a CloudEvent with no data payload
        }
        catch (JsonException exception)
        {
            return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                $"The CloudEvent is not valid JSON for '{bodyType.Name}'.", exception));
        }
        catch (FormatException exception)
        {
            return Result<object>.Fail(AppErrorFactory.SerializationFailed(
                $"The CloudEvent data payload could not be decoded into '{bodyType.Name}'.", exception));
        }
    }

    /// <summary>Fails for a CloudEvent that carries no decodable <c>data</c>.</summary>
    /// <param name="bodyType">The target runtime type, named in the failure message.</param>
    private static Result<object> EmptyData(Type bodyType) => Result<object>.Fail(
        AppErrorFactory.SerializationFailed($"The CloudEvent carries no data payload for '{bodyType.Name}'."));

    /// <summary>Wraps a decode that may have yielded null, which leaves nothing to hand a handler.</summary>
    /// <param name="body">The decoded body, or null.</param>
    /// <param name="bodyType">The target runtime type, named in the failure message.</param>
    private static Result<object> Decoded(object? body, Type bodyType) => body is null
        ? Result<object>.Fail(AppErrorFactory.SerializationFailed($"The payload decoded to null for '{bodyType.Name}'."))
        : Result<object>.Ok(body);
}
