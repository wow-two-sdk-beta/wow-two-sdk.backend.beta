using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;

/// <summary>Tuning for <see cref="CloudEventsMessageSerializer"/> — the producer identity and the optional context attributes it stamps.</summary>
/// <remarks>
/// <c>AddMessageSerializer&lt;T&gt;()</c> takes no options argument, so register this separately as a singleton before
/// swapping the serializer in; the serializer picks it up from DI.
/// </remarks>
public sealed class CloudEventsSerializerOptions
{
    /// <summary>
    /// The CloudEvents <c>source</c> — the context in which the event happened; REQUIRED by the spec and, paired with
    /// <c>id</c>, the consumer's duplicate-detection key. Defaults to <c>urn:wow-two:{entry-assembly-name}</c>.
    /// </summary>
    /// <remarks>Set this explicitly in production: the default identifies a process, not a logical service, so it moves when the host does.</remarks>
    public Uri Source { get; set; } = DefaultSource;

    /// <summary>JSON options for the <c>data</c> payload; null uses <see cref="JsonOptionsPresets.Default"/> so the body matches the SDK's JSON default byte-for-byte.</summary>
    public JsonSerializerOptions? Json { get; set; }

    /// <summary>
    /// Produces the CloudEvents <c>id</c> from the body; null generates a fresh GUID per serialize.
    /// </summary>
    /// <remarks>
    /// The seam hands the serializer no envelope, so the SDK's own <c>MessageId</c> is unreachable here and the default
    /// <c>id</c> is a value the SDK never sees again. Point this at a business key on the body (an order id, an
    /// aggregate version) whenever the consumer is expected to deduplicate on <c>source</c> + <c>id</c>.
    /// </remarks>
    public Func<object, Type, string>? IdFactory { get; set; }

    /// <summary>Optional CloudEvents <c>subject</c> — the specific object within the <see cref="Source"/> the event is about.</summary>
    public string? Subject { get; set; }

    /// <summary>Optional CloudEvents <c>dataschema</c> — a URI identifying the schema the <c>data</c> payload adheres to.</summary>
    public Uri? DataSchema { get; set; }

    /// <summary>Clock for the <c>time</c> attribute; defaults to <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider Clock { get; set; } = TimeProvider.System;

    private static Uri DefaultSource { get; } = new(
        "urn:wow-two:" + (Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown"),
        UriKind.RelativeOrAbsolute);
}

/// <summary>
/// <see cref="IMessageSerializer"/> that writes the CNCF <see href="https://cloudevents.io">CloudEvents</see> 1.0
/// <em>JSON event format</em> — the interop format that lets non-.NET producers and consumers share the SDK's streams
/// without agreeing on a CLR contract. Swap it in with
/// <c>services.AddMessageSerializer&lt;CloudEventsMessageSerializer&gt;()</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Structured mode, not binary mode.</b> CloudEvents defines two bindings: <em>binary</em> puts each attribute in its
/// own transport header (<c>ce-id</c>, <c>ce-source</c>, …) and leaves the body as the raw data; <em>structured</em>
/// encodes attributes and data together as one JSON document in the body. <see cref="IMessageSerializer"/> returns
/// bytes and cannot write transport headers — headers are stamped by each adapter from
/// <see cref="IMessageSerializer.ContentType"/> — so binary mode is not expressible at this seam. Structured mode is
/// also the self-describing one: the event survives a hop through a broker that drops unknown headers.
/// </para>
/// <para>
/// <b>Attribute mapping.</b> <c>specversion</c> = <c>1.0</c> (constant) · <c>type</c> =
/// <see cref="IMessageTypeResolver.ToTypeToken"/>, so <c>MapMessageType&lt;T&gt;("com.acme.orders.placed")</c> is how a
/// contract gets a reverse-DNS CloudEvents type instead of its CLR full name · <c>source</c> and the optional
/// <c>subject</c>/<c>dataschema</c> come from <see cref="CloudEventsSerializerOptions"/> · <c>time</c> is RFC 3339 UTC ·
/// <c>datacontenttype</c> is <c>application/json</c>, describing the inlined <c>data</c> value.
/// </para>
/// <para>
/// <b>What does not map.</b> <c>id</c> is generated here rather than taken from <c>EventEnvelope.MessageId</c>, and
/// inbound <c>id</c>/<c>source</c>/<c>time</c>/<c>subject</c> are parsed and dropped, because the seam passes only the
/// body in each direction. A consumer deduplicating on CloudEvents <c>source</c>+<c>id</c> and the SDK inbox
/// deduplicating on <c>wt-message-id</c> are therefore keyed on different values. See the seam notes in
/// <c>serialization.md</c>.
/// </para>
/// </remarks>
public sealed class CloudEventsMessageSerializer : IMessageSerializer
{
    /// <summary>The content type this serializer stamps — the CloudEvents JSON event format media type.</summary>
    public const string CloudEventsJsonContentType = "application/cloudevents+json";

    /// <summary>The CloudEvents specification version this serializer emits.</summary>
    public const string SpecVersion = "1.0";

    private const string DataContentType = "application/json";

    private readonly IMessageTypeResolver _typeResolver;
    private readonly CloudEventsSerializerOptions _options;
    private readonly JsonSerializerOptions _json;

    /// <summary>Create the serializer.</summary>
    /// <param name="typeResolver">Supplies the CloudEvents <c>type</c> attribute from the body's CLR type.</param>
    /// <param name="options">Producer identity and optional context attributes; null uses the defaults.</param>
    public CloudEventsMessageSerializer(IMessageTypeResolver typeResolver, CloudEventsSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(typeResolver);
        _typeResolver = typeResolver;
        _options = options ?? new CloudEventsSerializerOptions();
        _json = _options.Json ?? JsonOptionsPresets.Default;
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
    /// Reads the structured envelope and returns only the <c>data</c> payload — the context attributes have nowhere to
    /// go through this seam. A producer that sent <c>data_base64</c> (the spec's carrier for non-JSON data) is honoured:
    /// the decoded bytes are returned directly when <paramref name="bodyType"/> is <c>byte[]</c>, and otherwise
    /// parsed as JSON.
    /// </remarks>
    public object? Deserialize(ReadOnlySpan<byte> data, Type bodyType)
    {
        ArgumentNullException.ThrowIfNull(bodyType);
        if (data.IsEmpty)
            return null;

        var reader = new Utf8JsonReader(data);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A CloudEvents structured payload must be a JSON object.");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            if (reader.ValueTextEquals("data"u8))
            {
                reader.Read();
                return reader.TokenType == JsonTokenType.Null ? null : JsonSerializer.Deserialize(ref reader, bodyType, _json);
            }

            if (reader.ValueTextEquals("data_base64"u8))
            {
                reader.Read();
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                var decoded = reader.GetBytesFromBase64();
                if (bodyType == typeof(byte[]))
                    return decoded;

                return JsonSerializer.Deserialize(decoded, bodyType, _json);
            }

            reader.Read();
            reader.Skip(); // a context attribute this seam cannot surface — step over it, including nested extensions
        }

        return null; // a CloudEvent with no data payload
    }
}
