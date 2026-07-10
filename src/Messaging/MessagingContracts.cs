using System.Collections.ObjectModel;

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
}

/// <summary>The per-event context handed to a handler — the event, its envelope, and correlation-aware helpers.</summary>
/// <typeparam name="TEvent">Event contract type.</typeparam>
public sealed class EventContext<TEvent>
    where TEvent : class, IEvent
{
    private readonly IEventBus _bus;

    /// <summary>Create a context.</summary>
    /// <param name="event">The deserialized event.</param>
    /// <param name="envelope">The transport envelope.</param>
    /// <param name="bus">The bus, for correlation-propagating publish/send from within the handler.</param>
    public EventContext(TEvent @event, EventEnvelope envelope, IEventBus bus)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(bus);
        Event = @event;
        Envelope = envelope;
        _bus = bus;
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

    /// <summary>The event headers.</summary>
    public IReadOnlyDictionary<string, string> Headers => Envelope.Headers;

    /// <summary>Publish a follow-on event, auto-propagating correlation/conversation and setting this event as the cause.</summary>
    /// <typeparam name="TOut">Outgoing event type.</typeparam>
    /// <param name="event">The outgoing event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask PublishAsync<TOut>(TOut @event, CancellationToken cancellationToken = default)
        where TOut : class, IEvent
        => _bus.PublishAsync(
            @event,
            new PublishOptions { CorrelationId = CorrelationId ?? MessageId, ConversationId = ConversationId, CausationId = MessageId },
            cancellationToken);

    /// <summary>Send a follow-on event to a destination, auto-propagating correlation/conversation and setting this event as the cause.</summary>
    /// <typeparam name="TOut">Outgoing event type.</typeparam>
    /// <param name="destination">Destination (queue) name.</param>
    /// <param name="event">The outgoing event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask SendAsync<TOut>(string destination, TOut @event, CancellationToken cancellationToken = default)
        where TOut : class, IEvent
        => _bus.SendAsync(
            destination,
            @event,
            new SendOptions { CorrelationId = CorrelationId ?? MessageId, ConversationId = ConversationId, CausationId = MessageId },
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
