using System.Collections.ObjectModel;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>Optional base marker for a message contract. Plain POCOs are also accepted.</summary>
public interface IMessage;

/// <summary>Intent marker for a command — addressed to a single logical owner (use <see cref="IMessageBus.SendAsync{TMessage}"/>).</summary>
public interface ICommand : IMessage;

/// <summary>Intent marker for an event — broadcast to any number of subscribers (use <see cref="IMessageBus.PublishAsync{TMessage}"/>).</summary>
public interface IEvent : IMessage;

/// <summary>Handles a message of type <typeparamref name="TMessage"/>. Many handlers may handle the same message.</summary>
/// <typeparam name="TMessage">Message contract type.</typeparam>
public interface IMessageHandler<TMessage> where TMessage : class
{
    /// <summary>Handle the message. Throw to trigger retry / dead-lettering per the configured policy.</summary>
    /// <param name="context">The message and its envelope, plus correlation-aware publish/send helpers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask HandleAsync(MessageContext<TMessage> context, CancellationToken cancellationToken);
}

/// <summary>Transport-agnostic message bus. The in-memory transport is the zero-broker default; broker adapters implement the same surface.</summary>
public interface IMessageBus
{
    /// <summary>Publish an event — fan-out to all handlers of <typeparamref name="TMessage"/>.</summary>
    /// <typeparam name="TMessage">Message type.</typeparam>
    /// <param name="message">The message payload.</param>
    /// <param name="options">Optional publish options (ids, delay, headers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishAsync<TMessage>(TMessage message, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : class;

    /// <summary>Send a command to a named destination — point-to-point.</summary>
    /// <typeparam name="TMessage">Message type.</typeparam>
    /// <param name="destination">Logical destination (queue) name.</param>
    /// <param name="message">The message payload.</param>
    /// <param name="options">Optional send options (ids, delay, partition key, headers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SendAsync<TMessage>(string destination, TMessage message, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : class;
}

/// <summary>The transport envelope wrapping a message body with metadata, correlation, and reliability fields.</summary>
public sealed record MessageEnvelope
{
    /// <summary>Stable id — the idempotency / dedupe key.</summary>
    public required string MessageId { get; init; }

    /// <summary>The message payload.</summary>
    public required object Body { get; init; }

    /// <summary>Runtime type of <see cref="Body"/> — drives handler routing.</summary>
    public required Type BodyType { get; init; }

    /// <summary>Logical destination (queue/topic) name; empty for publish fan-out.</summary>
    public string Destination { get; init; } = string.Empty;

    /// <summary>Correlation id linking all messages in one business flow.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Conversation id linking a request/response exchange.</summary>
    public string? ConversationId { get; init; }

    /// <summary>Id of the message that caused this one to be produced.</summary>
    public string? CausationId { get; init; }

    /// <summary>Delivery attempt count — drives poison-message / dead-letter detection.</summary>
    public int DeliveryCount { get; init; }

    /// <summary>Earliest UTC time the message may be delivered (scheduled delivery); null = immediate.</summary>
    public DateTimeOffset? NotBeforeUtc { get; init; }

    /// <summary>Optional partition / ordering key.</summary>
    public string? PartitionKey { get; init; }

    /// <summary>Transport headers — carries W3C trace-context (<c>traceparent</c>) and custom metadata.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = ReadOnlyDictionary<string, string>.Empty;
}

/// <summary>The per-message context handed to a handler — the message, its envelope, and correlation-aware helpers.</summary>
/// <typeparam name="TMessage">Message contract type.</typeparam>
public sealed class MessageContext<TMessage>
    where TMessage : class
{
    private readonly IMessageBus _bus;

    /// <summary>Create a context.</summary>
    /// <param name="message">The deserialized message.</param>
    /// <param name="envelope">The transport envelope.</param>
    /// <param name="bus">The bus, for correlation-propagating publish/send from within the handler.</param>
    public MessageContext(TMessage message, MessageEnvelope envelope, IMessageBus bus)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(bus);
        Message = message;
        Envelope = envelope;
        _bus = bus;
    }

    /// <summary>The message payload.</summary>
    public TMessage Message { get; }

    /// <summary>The transport envelope.</summary>
    public MessageEnvelope Envelope { get; }

    /// <summary>The message id.</summary>
    public string MessageId => Envelope.MessageId;

    /// <summary>The correlation id, if any.</summary>
    public string? CorrelationId => Envelope.CorrelationId;

    /// <summary>The conversation id, if any.</summary>
    public string? ConversationId => Envelope.ConversationId;

    /// <summary>The message headers.</summary>
    public IReadOnlyDictionary<string, string> Headers => Envelope.Headers;

    /// <summary>Publish a follow-on event, auto-propagating correlation/conversation and setting this message as the cause.</summary>
    /// <typeparam name="TOut">Outgoing message type.</typeparam>
    /// <param name="message">The outgoing message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask PublishAsync<TOut>(TOut message, CancellationToken cancellationToken = default)
        where TOut : class
        => _bus.PublishAsync(
            message,
            new PublishOptions { CorrelationId = CorrelationId ?? MessageId, ConversationId = ConversationId, CausationId = MessageId },
            cancellationToken);

    /// <summary>Send a follow-on command to a destination, auto-propagating correlation/conversation and setting this message as the cause.</summary>
    /// <typeparam name="TOut">Outgoing message type.</typeparam>
    /// <param name="destination">Destination (queue) name.</param>
    /// <param name="message">The outgoing message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask SendAsync<TOut>(string destination, TOut message, CancellationToken cancellationToken = default)
        where TOut : class
        => _bus.SendAsync(
            destination,
            message,
            new SendOptions { CorrelationId = CorrelationId ?? MessageId, ConversationId = ConversationId, CausationId = MessageId },
            cancellationToken);
}

/// <summary>Optional options for <see cref="IMessageBus.PublishAsync{TMessage}"/>.</summary>
public sealed class PublishOptions
{
    /// <summary>Explicit message id (idempotency key). Generated when null.</summary>
    public string? MessageId { get; set; }

    /// <summary>Correlation id for the business flow.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Conversation id for a request/response exchange.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Id of the message that caused this one.</summary>
    public string? CausationId { get; set; }

    /// <summary>Delay before the message becomes deliverable (scheduled delivery).</summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>Custom headers to attach.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}

/// <summary>Optional options for <see cref="IMessageBus.SendAsync{TMessage}"/>.</summary>
public sealed class SendOptions
{
    /// <summary>Explicit message id (idempotency key). Generated when null.</summary>
    public string? MessageId { get; set; }

    /// <summary>Correlation id for the business flow.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Conversation id for a request/response exchange.</summary>
    public string? ConversationId { get; set; }

    /// <summary>Id of the message that caused this one.</summary>
    public string? CausationId { get; set; }

    /// <summary>Delay before the message becomes deliverable (scheduled delivery).</summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>Optional partition / ordering key.</summary>
    public string? PartitionKey { get; set; }

    /// <summary>Custom headers to attach.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; set; }
}
