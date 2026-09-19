using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Defines behavior that watches the receive path — notified once per received message, spanning the whole of processing including
/// settlement. Register with <c>AddMessageObservingInterceptor&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
///   - watches only — use <see cref="IConsumeInterceptor"/> to change the outcome
///   - a thrown exception is caught and logged, never propagated
///   - resolved as a singleton — must be thread-safe
/// </remarks>
public interface IReceiveObservingInterceptor
{
    /// <summary>A message arrived and is about to enter the filter chain.</summary>
    /// <param name="envelope">The message that arrived.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PreReceiveAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken);

    /// <summary>The message was processed and acknowledged — the terminal success hook.</summary>
    /// <param name="envelope">The message that arrived.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PostReceiveAsync(EventEnvelopeModel envelope, CancellationToken cancellationToken);

    /// <summary>Processing was exhausted and the message has been dead-lettered — the terminal failure hook, raised after settlement so the message is already at rest.</summary>
    /// <param name="envelope">The message that arrived.</param>
    /// <param name="exception">The terminal fault.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ReceiveFaultAsync(EventEnvelopeModel envelope, Exception exception, CancellationToken cancellationToken);
}
