using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

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
    public EventContext(TEvent @event, EventEnvelopeModel envelope, IEventBus bus, IMessageHeaderPropagationPolicy? headerPropagation = null)
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
    public EventEnvelopeModel Envelope { get; }

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
                // Caller-set only — the inbound ReplyTo is never inherited.
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
