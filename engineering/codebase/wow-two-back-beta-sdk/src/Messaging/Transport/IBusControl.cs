using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Runtime control over the consume side of the bus — pause, resume, and a bounded graceful stop, plus the state and
/// in-flight count behind them. Registered as a singleton by every transport registration path, so an ops endpoint, an
/// admin command, or a health check can resolve it.
/// </summary>
/// <remarks>
///   - pause is backpressure — nothing dropped or queued in-process
///   - gates entry only — a message in the pipeline runs to completion
///   - idempotent — a repeated pause, resume, or stop is a no-op
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
