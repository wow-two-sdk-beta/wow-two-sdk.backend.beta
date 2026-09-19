using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Policies;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Defines behavior that executes an event-handling action with resilience (retry/backoff, optionally circuit-breaker + timeout).
/// Owns the retry loop; **throws when the policy is exhausted** so the consumer can dead-letter. The default
/// impl uses <see cref="IRetryPolicy"/>; a Polly-backed impl (<c>AddPollyEventResilience</c>) swaps in exp+jitter
/// retry with a circuit breaker and timeout.
/// </summary>
/// <remarks>
///   - route every thrown exception through <see cref="IEventFaultPolicy"/> before spending an attempt
///   - honour the <see cref="FaultDisposition"/> verdict, delayed retry included (<c>Messaging.standard.md</c>)
///   - never classify cancellation — an <see cref="OperationCanceledException"/> from a cancelled token propagates as shutdown, never as a message fault
/// </remarks>
public interface IEventResiliencePipeline
{
    /// <summary>Execute <paramref name="action"/> under the configured resilience policy.</summary>
    /// <param name="action">The event-handling action (invoked once per attempt).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken);
}
