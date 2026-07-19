using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>
/// Default <see cref="IEventResiliencePipeline"/> — classifies the failure via <see cref="IEventFaultClassifier"/>,
/// then retries via the configured <see cref="IRetryPolicy"/> + <see cref="RetryConfig"/> and propagates once exhausted.
/// A <see cref="FaultDisposition.DeadLetter"/> verdict propagates without spending an attempt; an
/// <see cref="FaultDisposition.Ignore"/> verdict returns normally so the caller acknowledges.
/// Under <see cref="DelayedRetryOptions"/> the loop stops after one attempt and the caller re-enqueues instead.
/// </summary>
internal sealed class DefaultEventResiliencePipeline(
    IRetryPolicy retryPolicy,
    IOptions<InMemoryEventBusOptions> options,
    TimeProvider timeProvider,
    IEventFaultClassifier? classifier = null,
    DelayedRetryCoordinator? delayedRetry = null) : IEventResiliencePipeline
{
    // Optional so a hand-rolled composition that never registered a classifier still resolves — and gets today's
    // retry-everything behaviour rather than a container-time failure.
    private readonly IEventFaultClassifier _classifier = classifier ?? DefaultEventFaultClassifier.RetryAll;

    public async ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var config = options.Value.Retry;
        var attempt = 0;
        while (true)
        {
            try
            {
                await action(cancellationToken);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var disposition = _classifier.Classify(exception);
                if (disposition == FaultDisposition.Ignore)
                    return; // handled — the caller's success path acknowledges

                if (disposition == FaultDisposition.DeadLetter)
                    throw; // no redelivery would change the outcome — dead-letter now, budget untouched

                // Delayed retry spends the budget across deliveries instead of inside this loop: one attempt, then out,
                // so the caller settles the message and re-publishes it with a delay rather than sleeping on the
                // consumer slot. The propagated fault is what the caller classifies to make that decision.
                if (delayedRetry is { IsActive: true })
                    throw;

                attempt++;
                var delay = retryPolicy.NextDelay(attempt, config);
                if (delay is null)
                    throw;

                await Task.Delay(delay.Value, timeProvider, cancellationToken);
            }
        }
    }
}
