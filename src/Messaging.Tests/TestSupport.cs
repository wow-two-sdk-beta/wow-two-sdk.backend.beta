using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
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

/// <summary>An event for the harness suites — asserted through <c>MessagingTestHarness</c>, so its handler needs no collector.</summary>
public sealed record HarnessEvent(string Tag) : IEvent;

/// <summary>
/// Lets a test park a handler inside the pipeline and release it on command — the only way to observe in-flight state
/// deterministically, since anything time-based races the dispatch it is trying to measure.
/// </summary>
public sealed class HarnessGate
{
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _completed;

    /// <summary>When true the handler parks until <see cref="Release"/>; when false it returns immediately.</summary>
    public bool Hold { get; set; }

    /// <summary>Completes once a handler has entered — i.e. the message is genuinely in flight.</summary>
    public Task Started => _started.Task;

    /// <summary>How many handler invocations ran to completion.</summary>
    public int Completed => Volatile.Read(ref _completed);

    /// <summary>Release every parked handler.</summary>
    public void Release() => _release.TrySetResult();

    /// <summary>The handler body: announce entry, park if asked, then count the completion.</summary>
    /// <param name="cancellationToken">Cancellation token — host shutdown must be able to unwind a parked handler.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        if (Hold)
            await _release.Task.WaitAsync(cancellationToken);

        Interlocked.Increment(ref _completed);
    }
}

/// <summary>
/// Handles <see cref="HarnessEvent"/> through the optional <see cref="HarnessGate"/>.
/// </summary>
/// <remarks>
/// The gate is pulled from the provider rather than injected: this handler is scanned into every host in the assembly,
/// including the broker suites that never register a gate, and a constructor dependency they cannot satisfy would trip
/// container validation there.
/// </remarks>
public sealed class HarnessHandler(IServiceProvider services) : IEventHandler<HarnessEvent>
{
    public ValueTask HandleAsync(EventContext<HarnessEvent> context, CancellationToken cancellationToken)
    {
        var gate = services.GetService<HarnessGate>();
        return gate is null ? ValueTask.CompletedTask : new ValueTask(gate.RunAsync(cancellationToken));
    }
}
