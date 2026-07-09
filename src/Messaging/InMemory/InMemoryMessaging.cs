using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>Options for the in-memory event bus (the zero-broker default transport).</summary>
public sealed class InMemoryEventBusOptions
{
    /// <summary>Bounded channel capacity (backpressure when full). Set to 0 for an unbounded channel. Default 1024.</summary>
    public int ChannelCapacity { get; set; } = 1024;

    /// <summary>Retry schedule applied to failed handler invocations before dead-lettering.</summary>
    public RetryConfig Retry { get; set; } = new();
}

/// <summary>Single in-process channel backing the in-memory transport.</summary>
internal sealed class InMemoryEventChannel
{
    private readonly Channel<EventEnvelope> _channel;

    public InMemoryEventChannel(IOptions<InMemoryEventBusOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var capacity = options.Value.ChannelCapacity;
        _channel = capacity > 0
            ? Channel.CreateBounded<EventEnvelope>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
            })
            : Channel.CreateUnbounded<EventEnvelope>();
    }

    public ChannelReader<EventEnvelope> Reader => _channel.Reader;

    public ChannelWriter<EventEnvelope> Writer => _channel.Writer;
}

/// <summary>In-memory idempotency processor — dedupes by message id for the process lifetime; marks processed only on handler success (so retries re-run).</summary>
internal sealed class InMemoryInboxProcessor : IInboxProcessor
{
    private readonly ConcurrentDictionary<string, byte> _processed = new(StringComparer.Ordinal);

    public async ValueTask<bool> ProcessOnceAsync(string messageId, Func<CancellationToken, ValueTask> handler, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(handler);

        if (_processed.ContainsKey(messageId))
            return false;

        await handler(cancellationToken); // may throw → not marked → retried
        _processed.TryAdd(messageId, 0);
        return true;
    }
}

/// <summary>In-memory dead-letter store with replay back onto the channel.</summary>
internal sealed partial class InMemoryDeadLetterStore(InMemoryEventChannel channel, ILogger<InMemoryDeadLetterStore> logger) : IDeadLetterStore
{
    private readonly ConcurrentDictionary<string, DeadLetterRecord> _records = new(StringComparer.Ordinal);

    public ValueTask DeadLetterAsync(DeadLetterRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.MessageId] = record;
        LogDeadLettered(record.MessageId, record.Destination, record.Reason);
        return ValueTask.CompletedTask;
    }

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

    [LoggerMessage(EventId = 6001, Level = LogLevel.Error, Message = "Dead-lettered message {MessageId} from {Destination}: {Reason}")]
    private partial void LogDeadLettered(string messageId, string destination, string reason);
}

/// <summary>In-memory scheduler — delays via the time provider then enqueues onto the channel.</summary>
internal sealed partial class InMemoryEventScheduler(InMemoryEventChannel channel, TimeProvider timeProvider, ILogger<InMemoryEventScheduler> logger) : IEventScheduler
{
    public ValueTask ScheduleAsync(EventEnvelope envelope, DateTimeOffset notBeforeUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var delay = notBeforeUtc - timeProvider.GetUtcNow();
        if (delay <= TimeSpan.Zero)
            return channel.Writer.WriteAsync(envelope, cancellationToken);

        _ = DelayThenEnqueueAsync(envelope, delay, cancellationToken);
        return ValueTask.CompletedTask;
    }

    private async Task DelayThenEnqueueAsync(EventEnvelope envelope, TimeSpan delay, CancellationToken cancellationToken)
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

    [LoggerMessage(EventId = 6002, Level = LogLevel.Error, Message = "Scheduled enqueue failed for message {MessageId}")]
    private partial void LogScheduledEnqueueFailed(Exception exception, string messageId);
}
