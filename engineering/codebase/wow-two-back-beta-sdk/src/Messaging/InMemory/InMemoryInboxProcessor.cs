using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

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
