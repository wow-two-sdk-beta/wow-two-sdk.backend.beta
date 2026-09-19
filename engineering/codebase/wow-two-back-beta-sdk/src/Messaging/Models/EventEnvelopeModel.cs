using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Models;

/// <summary>Represents an event body with its delivery metadata, correlation and reliability hints.</summary>
public sealed record EventEnvelopeModel
{
    /// <summary>Stable id — the idempotency / dedupe key for the transport message.</summary>
    public required string MessageId { get; init; }

    /// <summary>The event payload.</summary>
    public required object Body { get; init; }

    /// <summary>Runtime type of <see cref="Body"/> — drives handler routing.</summary>
    public required Type BodyType { get; init; }

    /// <summary>Content type of the serialized body (e.g. <c>application/json</c>); carried as a wire header and used to select the deserializer on receive.</summary>
    public string ContentType { get; init; } = "application/json";

    /// <summary>Logical destination (queue/topic) name; empty for publish fan-out.</summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>
    /// Logical address a reply to this message should be sent to; null for a one-way message. Paired with
    /// <see cref="ConversationId"/>, which tells the requester which pending request the reply answers.
    /// </summary>
    /// <remarks>
    ///   - resolved by the adapter to its native reply mechanism (AMQP <c>reply-to</c>, a NATS inbox subject)
    ///   - carried as <see cref="Transport.MessageHeaderConstants.ReplyTo"/> where the transport has none
    ///   - the SDK correlates by it, never sends the reply itself
    /// </remarks>
    public string? ReplyTo { get; init; }

    /// <summary>Correlation id linking all events in one business flow.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Conversation id linking a request/response exchange.</summary>
    public string? ConversationId { get; init; }

    /// <summary>Id of the event that caused this one to be produced.</summary>
    public string? CausationId { get; init; }

    /// <summary>Delivery attempt count — drives poison-message / dead-letter detection.</summary>
    public int DeliveryCount { get; init; }

    /// <summary>Earliest UTC time the event may be delivered (scheduled delivery); null = immediate.</summary>
    public DateTimeOffset? NotBeforeUtc { get; init; }

    /// <summary>Optional partition / ordering key. Adapters map it to the transport's ordering primitive (Kafka partition, ASB session, …).</summary>
    public string? PartitionKey { get; init; }

    /// <summary>Transport-abstract hint: persist the message so it survives a broker restart. Adapters map it (RabbitMQ delivery-mode, ASB durability, …).</summary>
    public bool Durable { get; init; }

    /// <summary>Transport-abstract hint: relative priority. Adapters map it where supported (RabbitMQ priority queues, …); ignored otherwise.</summary>
    public int? Priority { get; init; }

    /// <summary>Transport-abstract hint: time-to-live after which the message expires undelivered. Adapters map it (RabbitMQ TTL, ASB TimeToLive, …).</summary>
    public TimeSpan? TimeToLive { get; init; }

    /// <summary>Transport headers — carries W3C trace-context (<c>traceparent</c>) and custom metadata.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>
    /// The body already serialized, for a send-path transformation that has to decide what goes on the wire before the
    /// adapter does. When set, an adapter puts these bytes on the wire verbatim instead of calling
    /// <see cref="IMessageSerializer.Serialize"/>; <see langword="null"/> — the default — leaves every adapter
    /// serializing <see cref="Body"/> itself.
    /// </summary>
    /// <remarks>
    ///   - set by a send-path transformation — claim check, payload compression, envelope encryption
    ///   - carry bytes a transformation already serialized to measure, rather than making the adapter repeat it
    ///   - <see cref="Body"/> and <see cref="BodyType"/> stay the logical message, so routing, metrics and the outbox are unaffected
    /// </remarks>
    public ReadOnlyMemory<byte>? RawBody { get; init; }

    /// <summary>
    /// The type the bytes in <see cref="RawBody"/> decode as, when the transformation put a different shape on
    /// the wire than <see cref="Body"/>. <see langword="null"/> — the default — means they still decode as
    /// <see cref="BodyType"/>.
    /// </summary>
    /// <remarks>
    ///   - set by a substituting transformation (claim check)
    ///   - left null by a transparent one (compression)
    ///   - governs the <see cref="MessageHeaderConstants.EventType"/> token, never routing
    /// </remarks>
    public Type? RawBodyType { get; init; }

    /// <summary>
    /// The type an adapter stamps as <see cref="MessageHeaderConstants.EventType"/>: the substituted wire type where there is
    /// one, else the logical <see cref="BodyType"/>. The receiving adapter deserializes the wire bytes into whatever
    /// this names, which is why it has to describe the shape actually on the wire rather than the logical one.
    /// </summary>
    public Type WireBodyType => RawBodyType ?? BodyType;

    /// <summary>
    /// The bytes to put on the wire: <see cref="RawBody"/> where a send-path transformation pre-serialized them, else
    /// <see cref="Body"/> run through <paramref name="serializer"/>. Every transport adapter calls this instead of
    /// serializing directly, so one expression governs whether a transformation is honoured.
    /// </summary>
    /// <param name="serializer">The serializer to fall back on when nothing pre-serialized the body.</param>
    public byte[] ToWireBody(IMessageSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        if (RawBody is not { } raw)
            return serializer.Serialize(Body, BodyType);

        // Hand back the producer's own array when it owns the whole buffer; copy only a slice of a larger one.
        return MemoryMarshal.TryGetArray(raw, out var segment) && segment.Array is { } array && segment.Offset == 0 && segment.Count == array.Length
            ? array
            : raw.ToArray();
    }
}
