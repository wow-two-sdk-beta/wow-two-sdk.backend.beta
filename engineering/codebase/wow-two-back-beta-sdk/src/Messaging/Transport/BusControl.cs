using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Default <see cref="IBusControl"/> — drives the <see cref="MessagePump"/>'s entry gate and reuses the pump's own
/// drain. Lifecycle state is stamped by <see cref="TransportConsumerBackgroundService"/>, which is what makes
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

    public BusControl(MessagePump pump, ConcurrencyOptions options, ILogger<BusControl> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _pump = pump;
        _options = options;
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

        // One budget for the whole stop, so the two bounded waits below cannot add up to more than DrainTimeout.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(_options.DrainTimeout);

        try
        {
            // The gate is shut, so in-flight only falls — no new arrival can top it up.
            await _pump.WaitForIdleAsync(budget.Token);
        }
        catch (OperationCanceledException)
        {
            LogStopTimedOut(_pump.InFlight, _options.DrainTimeout);
        }

        // Retire the worker loops — a no-op in sequential mode, near-instant in parallel mode.
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

    // Returns false when a stop already ran or is running, so a second caller never starts a second drain.
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
