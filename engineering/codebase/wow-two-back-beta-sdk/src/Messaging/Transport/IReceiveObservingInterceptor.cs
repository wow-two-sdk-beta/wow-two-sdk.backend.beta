using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Watches the receive path — notified <b>once per received message</b>, spanning the whole of processing including
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
    /// <param name="context">The receive context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PreReceiveAsync(ReceiveContext context, CancellationToken cancellationToken);

    /// <summary>The message was processed and acknowledged — the terminal success hook.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PostReceiveAsync(ReceiveContext context, CancellationToken cancellationToken);

    /// <summary>Processing was exhausted and the message has been dead-lettered — the terminal failure hook, raised after settlement so the message is already at rest.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The terminal fault.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ReceiveFaultAsync(ReceiveContext context, Exception exception, CancellationToken cancellationToken);
}
