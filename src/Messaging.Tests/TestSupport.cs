using System.Collections.Concurrent;
using System.Threading.Channels;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>An event whose handler succeeds and records receipt.</summary>
public sealed record PingEvent(string Value) : IEvent;

/// <summary>An event whose handler always throws (drives retry → dead-letter).</summary>
public sealed record BoomEvent(string Value) : IEvent;

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

/// <summary>Succeeds and records the event.</summary>
public sealed class PingHandler(EventCollector collector) : IEventHandler<PingEvent>
{
    public ValueTask HandleAsync(EventContext<PingEvent> context, CancellationToken cancellationToken)
    {
        collector.Record(context.Event);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Always throws — exercises retry then dead-lettering.</summary>
public sealed class BoomHandler : IEventHandler<BoomEvent>
{
    public ValueTask HandleAsync(EventContext<BoomEvent> context, CancellationToken cancellationToken)
        => throw new InvalidOperationException("boom");
}
