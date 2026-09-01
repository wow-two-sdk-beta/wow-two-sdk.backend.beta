using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// A manual-reset async gate: open by default, and while shut every waiter parks on one shared
/// <see cref="TaskCompletionSource"/> that <see cref="Open"/> completes in a single stroke.
/// </summary>
internal sealed class AsyncGate
{
    private readonly Lock _sync = new();

    // Non-null means shut — swapped to null before the source completes, so a waiter mid-Open sees an open gate.
    private TaskCompletionSource? _shut;

    /// <summary>True when messages are admitted.</summary>
    public bool IsOpen => Volatile.Read(ref _shut) is null;

    /// <summary>
    /// Park until the gate is open. Cancellation is honoured, which is what lets host shutdown unwind a parked consume
    /// loop: every transport treats an <see cref="OperationCanceledException"/> from the dispatch callback as a
    /// graceful stop.
    /// </summary>
    public ValueTask WaitAsync(CancellationToken cancellationToken)
    {
        var shut = Volatile.Read(ref _shut);
        if (shut is null)
            return ValueTask.CompletedTask;

        // No lost wakeup — Open between the read and here leaves this source already completed.
        return new ValueTask(shut.Task.WaitAsync(cancellationToken));
    }

    /// <summary>Shut the gate. Idempotent.</summary>
    public void Close()
    {
        lock (_sync)
        {
            // Asynchronous continuations: Open must not run every parked consume loop inline on the resuming ops thread.
            _shut ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Open the gate, releasing every parked waiter. Idempotent.</summary>
    public void Open()
    {
        TaskCompletionSource? shut;
        lock (_sync)
        {
            shut = _shut;
            _shut = null;
        }

        // Outside the lock: continuations must never run under it.
        shut?.TrySetResult();
    }
}
