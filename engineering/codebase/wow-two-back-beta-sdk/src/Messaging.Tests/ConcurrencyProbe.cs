using System.Collections.Concurrent;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Records peak overlap and per-key arrival order across handler invocations.</summary>
public sealed class ConcurrencyProbe
{
    private readonly ConcurrentQueue<string> _started = new();
    private readonly ConcurrentQueue<string> _completed = new();
    private int _current;
    private int _peak;

    /// <summary>How long each handler holds its slot.</summary>
    public TimeSpan Hold { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>The highest number of handlers observed running at the same instant.</summary>
    public int Peak => Volatile.Read(ref _peak);

    /// <summary>Tags in the order handlers began, which is dispatch order.</summary>
    public IReadOnlyCollection<string> Started => _started;

    /// <summary>Tags of handlers that ran to completion.</summary>
    public IReadOnlyCollection<string> Completed => _completed;

    /// <summary>Run one handler body: record entry, hold the slot, record completion.</summary>
    /// <param name="tag">The event's tag.</param>
    public async Task RunAsync(string tag)
    {
        _started.Enqueue(tag);
        var now = Interlocked.Increment(ref _current);
        var peak = Volatile.Read(ref _peak);
        while (now > peak && Interlocked.CompareExchange(ref _peak, now, peak) != peak)
            peak = Volatile.Read(ref _peak);

        try
        {
            await Task.Delay(Hold, CancellationToken.None);
            _completed.Enqueue(tag);
        }
        finally
        {
            Interlocked.Decrement(ref _current);
        }
    }
}
