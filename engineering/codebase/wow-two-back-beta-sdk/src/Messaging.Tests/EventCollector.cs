using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Collects received events and lets a test await a count.</summary>
public sealed class EventCollector
{
    private readonly ConcurrentQueue<object> _received = new();
    private readonly Channel<object> _signal = Channel.CreateUnbounded<object>();

    public int Count => _received.Count;

    public void Record(object @event)
    {
        _received.Enqueue(@event);
        _signal.Writer.TryWrite(@event);
    }

    public async Task<bool> WaitForCountAsync(int count, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            while (_received.Count < count)
                await _signal.Reader.ReadAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return _received.Count >= count;
        }
    }
}
