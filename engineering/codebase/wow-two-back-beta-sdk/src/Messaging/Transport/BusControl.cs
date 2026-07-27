using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Runtime state of the consume side of the bus — what <see cref="IBusControl.State"/> reports to an ops endpoint or a
/// health check.
/// </summary>
public enum BusState
{
    /// <summary>
    /// Not consuming. Either the host has not started the transport yet, or <see cref="IBusControl.StopAsync"/> has run
    /// and the consume workers were retired. Terminal once stopped — the bus does not restart in place.
    /// </summary>
    Stopped,

    /// <summary>Consuming normally — every received message flows into the processing pipeline.</summary>
    Running,

    /// <summary>
    /// Connected but not admitting. The transport stays subscribed while received messages park at the pipeline
    /// entrance, so nothing is dropped or buffered in-process and unconsumed messages stay unsettled at the broker,
    /// where prefetch throttles delivery. Work already in flight when the pause was requested runs to completion.
    /// </summary>
    Paused,

    /// <summary>Transient — inside <see cref="IBusControl.StopAsync"/>, waiting for in-flight messages to land before the bus reports <see cref="Stopped"/>.</summary>
    Draining,
}

/// <summary>
/// Runtime control over the consume side of the bus — pause, resume, and a bounded graceful stop, plus the state and
/// in-flight count behind them. Registered as a singleton by every transport registration path, so an ops endpoint, an
/// admin command, or a health check can resolve it.
/// </summary>
/// <remarks>
/// <para>
/// Pause is <b>backpressure, not buffering</b>: it shuts a gate in front of the message pump, so a paused consume loop
/// waits at the gate instead of pulling more work. Nothing is dropped, nothing is queued in-process, and unconsumed
/// messages stay unacknowledged at the broker — exactly the behaviour a broker's prefetch window is designed for.
/// </para>
/// <para>
/// Pause gates <i>entry</i> only. A message that had already entered the pipeline when the pause was requested runs to
/// completion; pause never abandons work. <see cref="InFlight"/> is how a caller observes those messages land.
/// </para>
/// <para>
/// Every method is idempotent — pausing a paused bus, resuming a running one, stopping a stopped one is a no-op, so a
/// retried ops call cannot fail. A transition that makes no sense (resuming a stopped bus) throws
/// <see cref="InvalidOperationException"/> rather than pretending to work.
/// </para>
/// </remarks>
public interface IBusControl
{
    /// <summary>The current state of the consume side.</summary>
    BusState State { get; }

    /// <summary>
    /// Messages currently being processed — queued to a consume worker or executing in a handler. Messages parked at
    /// the pause gate are <b>not</b> counted: they never entered the pipeline.
    /// </summary>
    int InFlight { get; }

    /// <summary>
    /// Stop admitting messages into the processing pipeline, leaving the transport connected. Returns as soon as the
    /// gate is shut — messages already in flight keep running, so poll <see cref="InFlight"/> to watch them land.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The bus is stopped or stopping.</exception>
    ValueTask PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>Admit messages again, releasing every consume loop parked at the gate.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The bus is stopped or stopping.</exception>
    ValueTask ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop consuming for good — shut the gate, wait for in-flight messages to land, then retire the consume workers,
    /// the whole sequence bounded by <see cref="ConcurrencyOptions.DrainTimeout"/>. Does not throw on timeout: an
    /// overrunning handler is logged and the bus reports <see cref="BusState.Stopped"/> either way.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token — caps the drain budget as well as cancelling the wait.</param>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Default <see cref="IBusControl"/> — drives the <see cref="MessagePump"/>'s entry gate and reuses the pump's own
/// drain. Lifecycle state is stamped by <see cref="TransportConsumerHostedService"/>, which is what makes
/// <see cref="State"/> reflect the host rather than only explicit control calls.
/// </summary>
internal sealed partial class BusControl : IBusControl
{
    private readonly MessagePump _pump;
    private readonly ConcurrencyOptions _options;
    private readonly ILogger<BusControl> _logger;
    private readonly Lock _sync = new();
    private int _state = (int)BusState.Stopped;
    private bool _stopRequested;

    public BusControl(MessagePump pump, IOptions<ConcurrencyOptions> options, ILogger<BusControl> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _pump = pump;
        _options = options.Value;
        _logger = logger;
    }

    // Int-backed so an ops endpoint polling State never contends with a transition.
    public BusState State => (BusState)Volatile.Read(ref _state);

    public int InFlight => _pump.InFlight;

    public ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            switch (State)
            {
                case BusState.Paused:
                    return ValueTask.CompletedTask; // idempotent — a retried ops call must not fail
                case BusState.Running:
                    break;
                default:
                    throw new InvalidOperationException($"The bus cannot be paused from {State}.");
            }

            // Gate first, state second: an observer that sees Paused can rely on the gate already being shut.
            _pump.Gate.Close();
            Volatile.Write(ref _state, (int)BusState.Paused);
        }

        LogPaused(_pump.InFlight);
        return ValueTask.CompletedTask;
    }

    public ValueTask ResumeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            switch (State)
            {
                case BusState.Running:
                    return ValueTask.CompletedTask; // idempotent
                case BusState.Paused:
                    break;
                default:
                    throw new InvalidOperationException($"The bus cannot be resumed from {State}.");
            }

            Volatile.Write(ref _state, (int)BusState.Running);
            _pump.Gate.Open(); // releases every parked consume loop at once
        }

        LogResumed();
        return ValueTask.CompletedTask;
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        if (!TryBeginStop())
            return;

        LogDraining(_pump.InFlight, _options.DrainTimeout);

        // One budget for the whole stop. Handing it to DrainAsync caps the pump's own DrainTimeout at whatever is left
        // of it, so the two bounded waits below can never add up to more than DrainTimeout in total.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.DrainTimeout);

        try
        {
            // The gate is shut, so in-flight only falls — it cannot be topped up by a new arrival, which is what makes
            // this terminate. This IS the whole drain in sequential mode, where work runs inline on the consume loop
            // and there are no worker loops to retire.
            await _pump.WaitForIdleAsync(budget.Token);
        }
        catch (OperationCanceledException)
        {
            LogStopTimedOut(_pump.InFlight, _options.DrainTimeout);
        }

        // Retire the worker loops through the pump's existing drain — a no-op in sequential mode, and near-instant in
        // parallel mode because the workers are already idle by the time we get here.
        await _pump.DrainAsync(budget.Token);

        Volatile.Write(ref _state, (int)BusState.Stopped);
        LogStopped();
    }

    /// <summary>Stamped by the transport hosted service when the consume loop starts.</summary>
    internal void MarkRunning()
    {
        lock (_sync)
        {
            // A stop that already ran wins — never resurrect a bus an operator killed, or one the host is tearing down.
            if (_stopRequested || State != BusState.Stopped)
                return;

            Volatile.Write(ref _state, (int)BusState.Running);
        }
    }

    /// <summary>Stamped by the transport hosted service once the transport has been released.</summary>
    internal void MarkStopped()
    {
        lock (_sync)
        {
            _stopRequested = true;
            Volatile.Write(ref _state, (int)BusState.Stopped);
        }
    }

    // Separated from StopAsync so the lock never sits in an async method. Returns false when a stop already ran or is
    // running — the second caller returns immediately rather than starting a second drain.
    private bool TryBeginStop()
    {
        lock (_sync)
        {
            if (_stopRequested)
                return false;

            _stopRequested = true;
            _pump.Gate.Close(); // nothing new enters the pipeline from here on
            Volatile.Write(ref _state, (int)BusState.Draining);
            return true;
        }
    }

    [LoggerMessage(EventId = 6031, Level = LogLevel.Information, Message = "Bus paused; {InFlight} messages still in flight will run to completion")]
    private partial void LogPaused(int inFlight);

    [LoggerMessage(EventId = 6032, Level = LogLevel.Information, Message = "Bus resumed")]
    private partial void LogResumed();

    [LoggerMessage(EventId = 6033, Level = LogLevel.Information, Message = "Bus stopping; draining {InFlight} in-flight messages within {DrainTimeout}")]
    private partial void LogDraining(int inFlight, TimeSpan drainTimeout);

    [LoggerMessage(EventId = 6034, Level = LogLevel.Warning, Message = "Bus stop drain timed out after {DrainTimeout} with {InFlight} messages still in flight")]
    private partial void LogStopTimedOut(int inFlight, TimeSpan drainTimeout);

    [LoggerMessage(EventId = 6035, Level = LogLevel.Information, Message = "Bus stopped")]
    private partial void LogStopped();
}

/// <summary>
/// A manual-reset async gate: open by default, and while shut every waiter parks on one shared
/// <see cref="TaskCompletionSource"/> that <see cref="Open"/> completes in a single stroke.
/// </summary>
/// <remarks>
/// <para>
/// The open path is a volatile read returning an already-completed <see cref="ValueTask"/> — no allocation, no state
/// machine suspension, so an un-paused pump pays the same as before the gate existed. Only the shut path allocates,
/// once per parked waiter.
/// </para>
/// <para>
/// <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/> is load-bearing: without it,
/// <see cref="Open"/> would run every parked consume loop's continuation inline on the resuming thread, which is an ops
/// endpoint's request thread.
/// </para>
/// </remarks>
internal sealed class AsyncGate
{
    private readonly Lock _sync = new();

    // Non-null means shut. The field is swapped to null before the source is completed, so a waiter arriving in the
    // middle of Open sees an open gate rather than racing the completion.
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

        // No lost wakeup: if Open runs between the read and here, this source is already completed and the await
        // returns immediately.
        return new ValueTask(shut.Task.WaitAsync(cancellationToken));
    }

    /// <summary>Shut the gate. Idempotent.</summary>
    public void Close()
    {
        lock (_sync)
        {
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
