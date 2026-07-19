namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Executes an event-handling action with resilience (retry/backoff, optionally circuit-breaker + timeout).
/// Owns the retry loop; **throws when the policy is exhausted** so the consumer can dead-letter. The default
/// impl uses <see cref="IRetryPolicy"/>; a Polly-backed impl (<c>AddPollyEventResilience</c>) swaps in exp+jitter
/// retry with a circuit breaker and timeout.
/// </summary>
/// <remarks>
/// Every implementation MUST route a thrown exception through the registered <see cref="IEventFaultClassifier"/>
/// before spending an attempt, and honour the verdict:
/// <list type="bullet">
///   <item><description><see cref="FaultDisposition.Retry"/> — retry as configured (the default for every exception, so an unconfigured pipeline behaves exactly as it did before classification existed).</description></item>
///   <item><description><see cref="FaultDisposition.DeadLetter"/> — rethrow at once, spending no attempt; the caller's failure path dead-letters it.</description></item>
///   <item><description><see cref="FaultDisposition.Ignore"/> — swallow and return normally, so the caller's success path acknowledges the message.</description></item>
/// </list>
/// Cancellation is not classified: an <see cref="OperationCanceledException"/> raised by a cancelled
/// <c>cancellationToken</c> always propagates as shutdown, never as a message fault.
/// </remarks>
public interface IEventResiliencePipeline
{
    /// <summary>Execute <paramref name="action"/> under the configured resilience policy.</summary>
    /// <param name="action">The event-handling action (invoked once per attempt).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken);
}
