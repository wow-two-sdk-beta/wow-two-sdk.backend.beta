using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// In-memory <see cref="IDeadLetterQueryStore"/> — the default in-memory store's behaviour plus the browse, lookup,
/// update and delete that <see cref="IDeadLetterAdmin"/> needs to do more than replay one message at a time.
/// </summary>
/// <remarks>
/// <para>
/// Transport-neutral despite the name: storage is in process, but replay goes through the registered
/// <see cref="ISendTransport"/>, so it also works behind a broker adapter whose DLQ the SDK owns rather than the broker
/// (Kafka's dead-letter topic, NATS' dead-letter subject). The default store writes the in-memory channel directly and
/// is therefore usable only with the in-memory transport.
/// </para>
/// <para>
/// Records live for the life of the process — the same durability as what it replaces. Nothing here is a substitute for
/// a broker's own DLQ or a durable store; it is the implementation that makes the administration surface exercisable
/// end-to-end without one.
/// </para>
/// </remarks>
internal sealed partial class InMemoryDeadLetterQueryStore(ISendTransport sendTransport, ILogger<InMemoryDeadLetterQueryStore> logger) : IDeadLetterQueryStore
{
    private readonly ConcurrentDictionary<string, DeadLetterRecord> _records = new(StringComparer.Ordinal);

    public ValueTask DeadLetterAsync(DeadLetterRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

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

    public async IAsyncEnumerable<DeadLetterRecord> QueryAsync(DeadLetterQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Ordered newest-first so a browse without a time filter shows the fresh failures an operator is looking for,
        // rather than whatever order the dictionary happens to hold.
        var ordered = _records.Values.OrderByDescending(static record => record.DeadLetteredAtUtc);
        var remaining = query.Limit > 0 ? query.Limit : int.MaxValue;

        foreach (var record in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!query.Matches(record))
                continue;

            if (remaining-- <= 0)
                break;

            yield return record;
        }

        await Task.CompletedTask;
    }

    public ValueTask<DeadLetterRecord?> FindAsync(string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(_records.TryGetValue(messageId, out var record) ? record : null);
    }

    public ValueTask<bool> UpdateAsync(DeadLetterRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        // Update, never insert: a record removed by a concurrent purge or replay must not be resurrected by an admin
        // write that was decided before it went.
        if (!_records.TryGetValue(record.MessageId, out var existing))
            return ValueTask.FromResult(false);

        return ValueTask.FromResult(_records.TryUpdate(record.MessageId, record, existing));
    }

    public ValueTask<bool> RemoveAsync(string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        cancellationToken.ThrowIfCancellationRequested();

        var removed = _records.TryRemove(messageId, out _);
        if (removed)
            LogPurged(messageId);

        return ValueTask.FromResult(removed);
    }

    public async ValueTask ReplayAsync(string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        // Removed first so two concurrent replays cannot both publish the same message; the loser gets nothing.
        if (!_records.TryRemove(messageId, out var record))
            return;

        try
        {
            // Replays what is stored, not a copy taken earlier — the admin writes the redrive marker onto the record
            // before calling this, and that is how the marker reaches the wire.
            await sendTransport.SendAsync(record.Envelope with { DeliveryCount = 0 }, cancellationToken);
        }
        catch (Exception)
        {
            // Put it back: a replay that failed to publish has neither delivered the message nor kept it, and losing a
            // poison message to a transient broker fault is the one outcome a dead-letter store must not have.
            _records.TryAdd(messageId, record);
            throw;
        }
    }

    [LoggerMessage(EventId = 6070, Level = LogLevel.Error, Message = "Dead-lettered message {MessageId} from {Destination}: {Reason}")]
    private partial void LogDeadLettered(string messageId, string destination, string reason);

    [LoggerMessage(EventId = 6079, Level = LogLevel.Information, Message = "Purged dead-letter record {MessageId}")]
    private partial void LogPurged(string messageId);
}
