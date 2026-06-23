using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>Options for the in-memory messaging transport (the zero-broker default).</summary>
public sealed class InMemoryMessagingOptions
{
    /// <summary>Bounded channel capacity (backpressure when full). Set to 0 for an unbounded channel. Default 1024.</summary>
    public int ChannelCapacity { get; set; } = 1024;

    /// <summary>Retry schedule applied to failed handler invocations before dead-lettering.</summary>
    public RetryConfig Retry { get; set; } = new();
}

/// <summary>Single in-process channel backing the in-memory transport.</summary>
internal sealed class InMemoryMessageChannel
{
    private readonly Channel<MessageEnvelope> _channel;

    public InMemoryMessageChannel(IOptions<InMemoryMessagingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var capacity = options.Value.ChannelCapacity;
        _channel = capacity > 0
            ? Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            })
            : Channel.CreateUnbounded<MessageEnvelope>();
    }

    public ChannelReader<MessageEnvelope> Reader => _channel.Reader;

    public ChannelWriter<MessageEnvelope> Writer => _channel.Writer;
}

/// <summary>Type-erased dispatcher base — resolves and invokes the handlers for one message type.</summary>
internal abstract class MessageDispatcher
{
    public abstract ValueTask DispatchAsync(IServiceProvider services, MessageEnvelope envelope, IMessageBus bus, CancellationToken cancellationToken);
}

/// <summary>Strongly-typed dispatcher for <typeparamref name="TMessage"/>.</summary>
internal sealed class MessageDispatcher<TMessage> : MessageDispatcher
    where TMessage : class
{
    public override async ValueTask DispatchAsync(IServiceProvider services, MessageEnvelope envelope, IMessageBus bus, CancellationToken cancellationToken)
    {
        var message = (TMessage)envelope.Body;
        var context = new MessageContext<TMessage>(message, envelope, bus);
        foreach (var handler in services.GetServices<IMessageHandler<TMessage>>())
            await handler.HandleAsync(context, cancellationToken);
    }
}

/// <summary>Maps a message type to its dispatcher. Built at registration time, resolved as a singleton.</summary>
internal sealed class MessageDispatcherRegistry
{
    private readonly Dictionary<Type, MessageDispatcher> _dispatchers = [];

    public void Register(Type messageType, MessageDispatcher dispatcher) => _dispatchers[messageType] = dispatcher;

    public bool Contains(Type messageType) => _dispatchers.ContainsKey(messageType);

    public bool TryGet(Type messageType, [MaybeNullWhen(false)] out MessageDispatcher dispatcher) => _dispatchers.TryGetValue(messageType, out dispatcher);
}

/// <summary>In-memory implementation of <see cref="IMessageBus"/> backed by a channel.</summary>
internal sealed class InMemoryMessageBus(InMemoryMessageChannel channel, IMessageScheduler scheduler, TimeProvider timeProvider) : IMessageBus
{
    public ValueTask PublishAsync<TMessage>(TMessage message, PublishOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentNullException.ThrowIfNull(message);
        return EnqueueAsync(message, typeof(TMessage).Name, options?.MessageId, options?.CorrelationId, options?.ConversationId, options?.CausationId, options?.Delay, partitionKey: null, options?.Headers, cancellationToken);
    }

    public ValueTask SendAsync<TMessage>(string destination, TMessage message, SendOptions? options = null, CancellationToken cancellationToken = default)
        where TMessage : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        ArgumentNullException.ThrowIfNull(message);
        return EnqueueAsync(message, destination, options?.MessageId, options?.CorrelationId, options?.ConversationId, options?.CausationId, options?.Delay, options?.PartitionKey, options?.Headers, cancellationToken);
    }

    private async ValueTask EnqueueAsync<TMessage>(
        TMessage message,
        string destination,
        string? messageId,
        string? correlationId,
        string? conversationId,
        string? causationId,
        TimeSpan? delay,
        string? partitionKey,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken cancellationToken)
        where TMessage : class
    {
        var now = timeProvider.GetUtcNow();
        var notBefore = delay is { } d && d > TimeSpan.Zero ? now + d : (DateTimeOffset?)null;
        var envelope = new MessageEnvelope
        {
            MessageId = messageId ?? Guid.NewGuid().ToString("N"),
            Body = message,
            BodyType = typeof(TMessage),
            Destination = destination,
            CorrelationId = correlationId,
            ConversationId = conversationId,
            CausationId = causationId,
            DeliveryCount = 0,
            NotBeforeUtc = notBefore,
            PartitionKey = partitionKey,
            Headers = headers ?? ReadOnlyDictionary<string, string>.Empty,
        };

        if (notBefore is { } when && when > now)
            await scheduler.ScheduleAsync(envelope, when, cancellationToken);
        else
            await channel.Writer.WriteAsync(envelope, cancellationToken);
    }
}

/// <summary>In-memory idempotency store — dedupes by message id for the process lifetime.</summary>
internal sealed class InMemoryInboxStore : IInboxStore
{
    private readonly ConcurrentDictionary<string, byte> _seen = new(StringComparer.Ordinal);

    public ValueTask<bool> TryBeginAsync(string messageId, CancellationToken cancellationToken) => ValueTask.FromResult(_seen.TryAdd(messageId, 0));
}

/// <summary>In-memory dead-letter store with replay back onto the channel.</summary>
internal sealed partial class InMemoryDeadLetterStore(InMemoryMessageChannel channel, ILogger<InMemoryDeadLetterStore> logger) : IDeadLetterStore
{
    private readonly ConcurrentDictionary<string, DeadLetterRecord> _records = new(StringComparer.Ordinal);

    public ValueTask DeadLetterAsync(DeadLetterRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.MessageId] = record;
        LogDeadLettered(record.MessageId, record.Destination, record.Reason);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(EventId = 6001, Level = LogLevel.Error, Message = "Dead-lettered message {MessageId} from {Destination}: {Reason}")]
    private partial void LogDeadLettered(string messageId, string destination, string reason);

    public async IAsyncEnumerable<DeadLetterRecord> ReadAsync(string source, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var record in _records.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(record.Destination, source, StringComparison.Ordinal))
                yield return record;
        }

        await Task.CompletedTask;
    }

    public ValueTask ReplayAsync(string messageId, CancellationToken cancellationToken)
    {
        if (_records.TryRemove(messageId, out var record))
            return channel.Writer.WriteAsync(record.Envelope with { DeliveryCount = 0 }, cancellationToken);

        return ValueTask.CompletedTask;
    }
}

/// <summary>In-memory scheduler — delays via the time provider then enqueues onto the channel.</summary>
internal sealed partial class InMemoryMessageScheduler(InMemoryMessageChannel channel, TimeProvider timeProvider, ILogger<InMemoryMessageScheduler> logger) : IMessageScheduler
{
    public ValueTask ScheduleAsync(MessageEnvelope envelope, DateTimeOffset notBeforeUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var delay = notBeforeUtc - timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
            return channel.Writer.WriteAsync(envelope, cancellationToken);

        _ = DelayThenEnqueueAsync(envelope, delay, cancellationToken);
        return ValueTask.CompletedTask;
    }

    private async Task DelayThenEnqueueAsync(MessageEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, timeProvider, cancellationToken);
            await channel.Writer.WriteAsync(envelope, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // host shutting down — drop the scheduled delivery
        }
        catch (Exception ex)
        {
            LogScheduledEnqueueFailed(ex, envelope.MessageId);
        }
    }

    [LoggerMessage(EventId = 6011, Level = LogLevel.Error, Message = "Scheduled enqueue failed for message {MessageId}")]
    private partial void LogScheduledEnqueueFailed(Exception exception, string messageId);
}

/// <summary>Drains the channel, dispatches each message inside a fresh DI scope, and applies retry → dead-letter.</summary>
internal sealed partial class MessageConsumerHostedService(
    InMemoryMessageChannel channel,
    IServiceScopeFactory scopeFactory,
    MessageDispatcherRegistry registry,
    IMessageBus bus,
    IRetryPolicy retryPolicy,
    IDeadLetterStore deadLetters,
    IInboxStore inbox,
    TimeProvider timeProvider,
    IOptions<InMemoryMessagingOptions> options,
    ILogger<MessageConsumerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var envelope in channel.Reader.ReadAllAsync(stoppingToken))
                await ProcessAsync(envelope, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task ProcessAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!await inbox.TryBeginAsync(envelope.MessageId, cancellationToken))
        {
            LogDuplicateSkipped(envelope.MessageId);
            return;
        }

        var config = options.Value.Retry;
        var attempt = 0;
        while (true)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                if (registry.TryGet(envelope.BodyType, out var dispatcher))
                    await dispatcher.DispatchAsync(scope.ServiceProvider, envelope, bus, cancellationToken);
                else
                    LogNoHandler(envelope.BodyType.FullName);

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = retryPolicy.NextDelay(attempt, config);
                if (delay is null)
                {
                    await deadLetters.DeadLetterAsync(
                        DeadLetterRecord.From(envelope with { DeliveryCount = attempt }, ex, timeProvider.GetUtcNow()),
                        cancellationToken);
                    return;
                }

                LogAttemptFailed(ex, envelope.MessageId, envelope.Destination, attempt, delay);
                await Task.Delay(delay.Value, timeProvider, cancellationToken);
            }
        }
    }

    [LoggerMessage(EventId = 6021, Level = LogLevel.Debug, Message = "Skipping duplicate message {MessageId}")]
    private partial void LogDuplicateSkipped(string messageId);

    [LoggerMessage(EventId = 6022, Level = LogLevel.Warning, Message = "No handler registered for message type {MessageType}")]
    private partial void LogNoHandler(string? messageType);

    [LoggerMessage(EventId = 6023, Level = LogLevel.Warning, Message = "Message {MessageId} ({Destination}) attempt {Attempt} failed; retrying in {Delay}")]
    private partial void LogAttemptFailed(Exception exception, string messageId, string destination, int attempt, TimeSpan? delay);
}
