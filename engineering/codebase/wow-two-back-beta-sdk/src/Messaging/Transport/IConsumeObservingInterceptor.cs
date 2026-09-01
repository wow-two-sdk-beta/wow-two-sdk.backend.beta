using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Watches dispatch — notified <b>once per delivery attempt</b>, inside the resilience loop, around dedupe and handler
/// dispatch. A retried message notifies these hooks again per attempt. Register with <c>AddMessageObservingInterceptor&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
///   - watches only — use <see cref="IConsumeInterceptor"/> to change the outcome
///   - a thrown exception is caught and logged — the message still retries and dead-letters
///   - resolved as a singleton — must be thread-safe
/// </remarks>
public interface IConsumeObservingInterceptor
{
    /// <summary>A delivery attempt is starting, before the inbox dedupe check and handler dispatch.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PreConsumeAsync(ReceiveContext context, CancellationToken cancellationToken);

    /// <summary>The attempt completed without throwing, with the outcome that was recorded to metrics.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="outcome">How the attempt ended — dispatched, skipped as a duplicate, or unhandled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PostConsumeAsync(ReceiveContext context, ConsumeOutcome outcome, CancellationToken cancellationToken);

    /// <summary>The attempt threw. The fault still propagates into the retry/dead-letter decision. Not raised when the attempt is cancelled through its own token.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The fault thrown by the attempt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ConsumeFaultAsync(ReceiveContext context, Exception exception, CancellationToken cancellationToken);
}
