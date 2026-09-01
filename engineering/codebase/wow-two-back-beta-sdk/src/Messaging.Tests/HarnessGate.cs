using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

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
