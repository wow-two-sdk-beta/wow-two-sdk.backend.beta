using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>
/// Marker for an event contract — a fact that happened, carried by the <see cref="IEventBus"/>.
/// Only events cross the bus; commands and queries stay on the in-process mediator. Delivery topology
/// (in-memory vs broker) is a wiring choice and is deliberately absent from the contract.
/// </summary>
public interface IEvent;

/// <summary>Handles an event of type <typeparamref name="TEvent"/>. Many handlers may handle the same event (fan-out).</summary>
/// <typeparam name="TEvent">Event contract type.</typeparam>
public interface IEventHandler<TEvent>
    where TEvent : class, IEvent
{
    /// <summary>Handle the event. Throw to trigger retry / dead-lettering per the configured policy.</summary>
    /// <param name="context">The event and its envelope, plus correlation-aware publish/send helpers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask HandleAsync(EventContext<TEvent> context, CancellationToken cancellationToken);
}

/// <summary>
/// The event bus — the sole surface for asynchronous, at-least-once event delivery. The in-memory transport
/// is the zero-broker default; broker adapters implement the same surface. It accepts <see cref="IEvent"/> only.
/// </summary>
public interface IEventBus
{
    /// <summary>Publish an event — fan-out to every handler of <typeparamref name="TEvent"/>.</summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="event">The event payload.</param>
    /// <param name="options">Optional publish options (ids, delay, transport hints, headers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishAsync<TEvent>(TEvent @event, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;

    /// <summary>Send an event to a named destination — point-to-point (delivered to that destination's consumer, not fanned out).</summary>
    /// <typeparam name="TEvent">Event type.</typeparam>
    /// <param name="destination">Logical destination (queue) name.</param>
    /// <param name="event">The event payload.</param>
    /// <param name="options">Optional send options (ids, delay, partition key, transport hints, headers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SendAsync<TEvent>(string destination, TEvent @event, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}

/// <summary>The transport envelope wrapping an event body with metadata, correlation, reliability, and transport-abstract hints.</summary>
public sealed record EventEnvelope
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
    /// The address is transport-abstract — a queue/subject name, resolved by the adapter to its native reply mechanism
    /// (AMQP <c>reply-to</c>, a NATS inbox subject) or carried as <see cref="Transport.MessageHeaders.ReplyTo"/> where
    /// none exists. The SDK carries it and correlates by it; it does not itself send replies.
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
    /// serializing <see cref="Body"/> itself, exactly as before this existed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A general seam, not one feature's field. Three transformations need to substitute wire bytes without disturbing
    /// routing, and they are the same shape: claim check (the body is replaced by a pointer to blob storage), payload
    /// compression (the same body, deflated) and envelope encryption (the same body, as ciphertext). Each runs once on
    /// the send path and hands the adapter the bytes it produced.
    /// </para>
    /// <para>
    /// It also removes a double serialization. A transformation that has to <i>measure</i> the body — a size threshold,
    /// a compress-only-if-it-helps check — has already serialized it, and carrying those bytes here spends that work
    /// rather than making the adapter repeat it.
    /// </para>
    /// <para>
    /// <see cref="Body"/> and <see cref="BodyType"/> stay the logical message throughout, so routing, metrics,
    /// observers and the outbox keep describing what was published rather than how it was encoded.
    /// </para>
    /// </remarks>
    public ReadOnlyMemory<byte>? RawBody { get; init; }

    /// <summary>
    /// The type the bytes in <see cref="RawBody"/> decode as, when the transformation put a <i>different shape</i> on
    /// the wire than <see cref="Body"/>. <see langword="null"/> — the default — means they still decode as
    /// <see cref="BodyType"/>.
    /// </summary>
    /// <remarks>
    /// Set by a substituting transformation (claim check sends a claim reference; envelope encryption would send its
    /// ciphertext wrapper), left null by a transparent one (compression re-encodes the same contract). It governs the
    /// <see cref="MessageHeaders.EventType"/> token only — see <see cref="WireBodyType"/> — and never routing, which
    /// stays on <see cref="BodyType"/> so the message still lands where consumers of the real contract are bound.
    /// </remarks>
    public Type? RawBodyType { get; init; }

    /// <summary>
    /// The type an adapter stamps as <see cref="MessageHeaders.EventType"/>: the substituted wire type where there is
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

        // Hand back the producer's own array when it owns the whole buffer, so the seam allocates nothing on the common
        // path; only a slice of a larger (pooled) buffer has to be copied out.
        return MemoryMarshal.TryGetArray(raw, out var segment) && segment.Array is { } array && segment.Offset == 0 && segment.Count == array.Length
            ? array
            : raw.ToArray();
    }
}

/// <summary>The per-event context handed to a handler — the event, its envelope, and correlation-aware helpers.</summary>
/// <typeparam name="TEvent">Event contract type.</typeparam>
public sealed class EventContext<TEvent>
    where TEvent : class, IEvent
{
    private readonly IEventBus _bus;
    private readonly IMessageHeaderPropagationPolicy _headerPropagation;

    /// <summary>Create a context.</summary>
    /// <param name="event">The deserialized event.</param>
    /// <param name="envelope">The transport envelope.</param>
    /// <param name="bus">The bus, for correlation-propagating publish/send from within the handler.</param>
    /// <param name="headerPropagation">
    /// Decides which of this message's headers flow onto messages published from this context.
    /// <see cref="MessageHeaderPropagationPolicy.Default"/> (W3C trace context only) when null.
    /// </param>
    public EventContext(TEvent @event, EventEnvelope envelope, IEventBus bus, IMessageHeaderPropagationPolicy? headerPropagation = null)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(bus);
        Event = @event;
        Envelope = envelope;
        _bus = bus;
        _headerPropagation = headerPropagation ?? MessageHeaderPropagationPolicy.Default;
    }

    /// <summary>The event payload.</summary>
    public TEvent Event { get; }

    /// <summary>The transport envelope.</summary>
    public EventEnvelope Envelope { get; }

    /// <summary>The transport message id.</summary>
    public string MessageId => Envelope.MessageId;

    /// <summary>The correlation id, if any.</summary>
    public string? CorrelationId => Envelope.CorrelationId;

    /// <summary>The conversation id, if any.</summary>
    public string? ConversationId => Envelope.ConversationId;

    /// <summary>The address a reply to this message should be sent to, if any.</summary>
    public string? ReplyTo => Envelope.ReplyTo;

    /// <summary>The event headers.</summary>
    public IReadOnlyDictionary<string, string> Headers => Envelope.Headers;

    /// <summary>Publish a follow-on event, auto-propagating correlation/conversation/headers and setting this event as the cause.</summary>
    /// <typeparam name="TOut">Outgoing event type.</typeparam>
    /// <param name="event">The outgoing event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask PublishAsync<TOut>(TOut @event, CancellationToken cancellationToken = default)
        where TOut : class, IEvent
        => PublishAsync(@event, options: null, cancellationToken);

    /// <summary>
    /// Publish a follow-on event with explicit options, auto-propagating correlation/conversation/headers and setting
    /// this event as the cause. Any field set on <paramref name="options"/> overrides what would be propagated.
    /// </summary>
    /// <typeparam name="TOut">Outgoing event type.</typeparam>
    /// <param name="event">The outgoing event.</param>
    /// <param name="options">Publish options; null behaves exactly like the overload without them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask PublishAsync<TOut>(TOut @event, PublishOptions? options, CancellationToken cancellationToken = default)
        where TOut : class, IEvent
        => _bus.PublishAsync(
            @event,
            new PublishOptions
            {
                MessageId = options?.MessageId,
                CorrelationId = options?.CorrelationId ?? CorrelationId ?? MessageId,
                ConversationId = options?.ConversationId ?? ConversationId,
                CausationId = options?.CausationId ?? MessageId,
                Delay = options?.Delay,
                PartitionKey = options?.PartitionKey,
                Durable = options?.Durable,
                Priority = options?.Priority,
                TimeToLive = options?.TimeToLive,
                // Caller-set only — unlike correlation, the inbound ReplyTo is never inherited. A reply address belongs
                // to the message that carried it; forwarding it would aim a follow-on event's replies at a requester
                // waiting on a different exchange.
                ReplyTo = options?.ReplyTo,
                Headers = _headerPropagation.BuildOutboundHeaders(Envelope.Headers, options?.Headers),
            },
            cancellationToken);

    /// <summary>Send a follow-on event to a destination, auto-propagating correlation/conversation/headers and setting this event as the cause.</summary>
    /// <typeparam name="TOut">Outgoing event type.</typeparam>
    /// <param name="destination">Destination (queue) name.</param>
    /// <param name="event">The outgoing event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask SendAsync<TOut>(string destination, TOut @event, CancellationToken cancellationToken = default)
        where TOut : class, IEvent
        => SendAsync(destination, @event, options: null, cancellationToken);

    /// <summary>
    /// Send a follow-on event to a destination with explicit options, auto-propagating correlation/conversation/headers
    /// and setting this event as the cause. Any field set on <paramref name="options"/> overrides what would be propagated.
    /// </summary>
    /// <typeparam name="TOut">Outgoing event type.</typeparam>
    /// <param name="destination">Destination (queue) name.</param>
    /// <param name="event">The outgoing event.</param>
    /// <param name="options">Send options; null behaves exactly like the overload without them.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask SendAsync<TOut>(string destination, TOut @event, SendOptions? options, CancellationToken cancellationToken = default)
        where TOut : class, IEvent
        => _bus.SendAsync(
            destination,
            @event,
            new SendOptions
            {
                MessageId = options?.MessageId,
                CorrelationId = options?.CorrelationId ?? CorrelationId ?? MessageId,
                ConversationId = options?.ConversationId ?? ConversationId,
                CausationId = options?.CausationId ?? MessageId,
                Delay = options?.Delay,
                PartitionKey = options?.PartitionKey,
                Durable = options?.Durable,
                Priority = options?.Priority,
                TimeToLive = options?.TimeToLive,
                ReplyTo = options?.ReplyTo,
                Headers = _headerPropagation.BuildOutboundHeaders(Envelope.Headers, options?.Headers),
            },
            cancellationToken);
}

/// <summary>Optional options for <see cref="IEventBus.PublishAsync{TEvent}"/>. Transport hints are abstract — each adapter maps them to its native mechanism or ignores them.</summary>
public sealed class PublishOptions
{
    /// <summary>Explicit message id (idempotency key). Generated when null.</summary>
    public string? MessageId { get; set; }

    /// <summary>Correlation id for the business flow.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Conversation id for a request/response exchange.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Address a reply should be sent to (transport-abstract); null for a one-way message. See <see cref="EventEnvelope.ReplyTo"/>.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Id of the event that caused this one.</summary>
    public string? CausationId { get; set; }

    /// <summary>Delay before the event becomes deliverable (scheduled delivery).</summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>Partition / ordering key (transport-abstract).</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Persist the message so it survives a broker restart (transport-abstract).</summary>
    public bool? Durable { get; set; }

    /// <summary>Relative priority hint (transport-abstract).</summary>
    public int? Priority { get; set; }

    /// <summary>Time-to-live after which the message expires undelivered (transport-abstract).</summary>
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Custom headers to attach.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}

/// <summary>Optional options for <see cref="IEventBus.SendAsync{TEvent}"/>. Transport hints are abstract — each adapter maps them to its native mechanism or ignores them.</summary>
public sealed class SendOptions
{
    /// <summary>Explicit message id (idempotency key). Generated when null.</summary>
    public string? MessageId { get; set; }

    /// <summary>Correlation id for the business flow.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Conversation id for a request/response exchange.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Address a reply should be sent to (transport-abstract); null for a one-way message. See <see cref="EventEnvelope.ReplyTo"/>.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Id of the event that caused this one.</summary>
    public string? CausationId { get; set; }

    /// <summary>Delay before the event becomes deliverable (scheduled delivery).</summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>Partition / ordering key (transport-abstract).</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Persist the message so it survives a broker restart (transport-abstract).</summary>
    public bool? Durable { get; set; }

    /// <summary>Relative priority hint (transport-abstract).</summary>
    public int? Priority { get; set; }

    /// <summary>Time-to-live after which the message expires undelivered (transport-abstract).</summary>
    public TimeSpan? TimeToLive { get; set; }

    /// <summary>Custom headers to attach.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}
