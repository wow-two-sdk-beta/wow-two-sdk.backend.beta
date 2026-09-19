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
    private readonly Dictionary<string, MessageGate> _gates = new(StringComparer.Ordinal);

    public async ValueTask<bool> ProcessOnceAsync(string messageId, Func<CancellationToken, ValueTask> handler, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentNullException.ThrowIfNull(handler);

        var gate = RentGate(messageId);
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_processed.ContainsKey(messageId))
                    return false;

                await handler(cancellationToken); // may throw → not marked → retried
                _processed.TryAdd(messageId, 0);
                return true;
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }
        finally
        {
            ReturnGate(messageId, gate);
        }
    }

    private MessageGate RentGate(string messageId)
    {
        lock (_gates)
        {
            if (!_gates.TryGetValue(messageId, out var gate))
            {
                gate = new MessageGate();
                _gates.Add(messageId, gate);
            }

            gate.Users++;
            return gate;
        }
    }

    private void ReturnGate(string messageId, MessageGate gate)
    {
        lock (_gates)
        {
            if (--gate.Users != 0)
                return;

            _gates.Remove(messageId);
            gate.Dispose();
        }
    }

    private sealed class MessageGate : IDisposable
    {
        internal readonly SemaphoreSlim Semaphore = new(1, 1);
        internal int Users;

        public void Dispose() => Semaphore.Dispose();
    }
}
