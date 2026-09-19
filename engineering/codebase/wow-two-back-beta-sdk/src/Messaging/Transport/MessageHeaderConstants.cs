using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Holds wire-header conventions shared by every broker adapter — the reserved control namespace, the SDK's own control
/// header names, and the standard context headers the SDK understands. Adapters name headers from here rather than
/// from inline literals, so one constant governs the wire contract on every broker.
/// </summary>
public static class MessageHeaderConstants
{
    /// <summary>
    /// Holds prefix reserved for SDK control headers — type token, content type, partition key, dead-letter death info.
    /// A caller-supplied header using this prefix is dropped on send so it cannot forge the wire contract.
    /// </summary>
    public const string ReservedPrefix = "wt-";

    /// <summary>Holds the reserved stable type token of the body.</summary>
    public const string EventType = ReservedPrefix + "event-type";

    /// <summary>Holds the reserved content type of the serialized body.</summary>
    public const string ContentType = ReservedPrefix + "content-type";

    /// <summary>Holds the reserved <see cref="EventEnvelopeModel.MessageId"/> header.</summary>
    public const string MessageId = ReservedPrefix + "message-id";

    /// <summary>Holds the reserved <see cref="EventEnvelopeModel.PartitionKey"/> header.</summary>
    public const string PartitionKey = ReservedPrefix + "partition-key";

    /// <summary>
    /// Holds reserved. The envelope's <see cref="EventEnvelopeModel.ReplyTo"/> address, for brokers with no native reply-address
    /// property. RabbitMQ maps the native AMQP <c>reply-to</c> property instead and reads this only as a fallback.
    /// </summary>
    public const string ReplyTo = ReservedPrefix + "reply-to";

    /// <summary>
    /// Holds reserved. The envelope's <see cref="EventEnvelopeModel.CorrelationId"/>, linking every message in one business flow.
    /// Distinct from <see cref="ConversationId"/>, which pairs a single reply with its request.
    /// </summary>
    public const string CorrelationId = ReservedPrefix + "correlation-id";

    /// <summary>Holds the reserved <see cref="EventEnvelopeModel.ConversationId"/> header.</summary>
    public const string ConversationId = ReservedPrefix + "conversation-id";

    /// <summary>
    /// Holds reserved. The envelope's <see cref="EventEnvelopeModel.DeliveryCount"/>, for brokers with no per-message redelivery
    /// counter of their own — a Kafka record is immutable and its offset says nothing about how often it was handed to
    /// a consumer, so the count has to travel with the message and be bumped by whoever re-produces it.
    /// </summary>
    public const string DeliveryCount = ReservedPrefix + "delivery-count";

    /// <summary>Holds the reserved dead-letter reason header.</summary>
    public const string DeadLetterReason = ReservedPrefix + "dl-reason";

    /// <summary>Holds the reserved terminal-exception type header.</summary>
    public const string DeadLetterExceptionType = ReservedPrefix + "dl-exception-type";

    /// <summary>
    /// Holds w3C trace-context <c>traceparent</c>. Not reserved — it is a standard name, not an SDK-owned one. Stamped on
    /// send from the ambient producer <see cref="System.Diagnostics.Activity"/> and read back on receive to parent the
    /// consumer span.
    /// </summary>
    public const string TraceParent = "traceparent";

    /// <summary>Holds w3C trace-context <c>tracestate</c>, carried alongside <see cref="TraceParent"/>.</summary>
    public const string TraceState = "tracestate";

    /// <summary>
    /// Holds w3C <c>baggage</c> — user-defined context travelling with the trace. Neither stamped nor propagated by default;
    /// add it to an <see cref="IMessageHeaderPropagationPolicy"/> to opt in.
    /// </summary>
    public const string Baggage = "baggage";

    /// <summary>True when the key belongs to the SDK's reserved control namespace and is owned by the SDK, not the caller.</summary>
    /// <param name="key">The header key.</param>
    public static bool IsReserved(string key) => key is not null && key.StartsWith(ReservedPrefix, StringComparison.Ordinal);

    /// <summary>
    /// True when a transport adapter re-stamps this key from the envelope on every send, so a caller-supplied copy must
    /// be dropped rather than allowed to forge the wire contract.
    /// </summary>
    /// <remarks>
    ///   - strip only these keys on send, never the whole <c>wt-</c> namespace
    ///   - reserved feature headers — retry tier, redrive count, claim check — are never re-stamped
    ///   - stripping one silently disables its feature behind every broker
    /// </remarks>
    /// <param name="key">The header key.</param>
    public static bool IsAdapterOwned(string key) => key is not null && (
        key == EventType || key == ContentType || key == MessageId || key == PartitionKey ||
        key == ReplyTo || key == CorrelationId || key == ConversationId || key == DeliveryCount);
}
