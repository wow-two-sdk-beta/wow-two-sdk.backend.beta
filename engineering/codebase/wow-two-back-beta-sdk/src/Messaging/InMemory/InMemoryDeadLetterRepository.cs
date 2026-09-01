using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>In-memory dead-letter store with replay back onto the channel.</summary>
internal sealed partial class InMemoryDeadLetterRepository(InMemoryEventChannel channel, ILogger<InMemoryDeadLetterRepository> logger) : IDeadLetterRepository
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
