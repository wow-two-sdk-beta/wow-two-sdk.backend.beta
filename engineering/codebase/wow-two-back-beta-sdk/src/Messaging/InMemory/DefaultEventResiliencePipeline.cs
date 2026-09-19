using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Policies;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Services;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>
/// Default <see cref="IEventResiliencePipeline"/> — classifies the failure via <see cref="IEventFaultPolicy"/>,
/// then retries via the configured <see cref="IRetryPolicy"/> + <see cref="RetryConfig"/> and propagates once exhausted.
/// A <see cref="FaultDisposition.DeadLetter"/> verdict propagates without spending an attempt; an
/// <see cref="FaultDisposition.Ignore"/> verdict returns normally so the caller acknowledges.
/// Under <see cref="DelayedRetryOptions"/> the loop stops after one attempt and the caller re-enqueues instead.
/// </summary>
internal sealed class DefaultEventResiliencePipeline(
    IRetryPolicy retryPolicy,
    InMemoryEventBusOptions options,
    TimeProvider timeProvider,
    IEventFaultPolicy? faultPolicy = null,
    DelayedRetryService? delayedRetry = null) : IEventResiliencePipeline
{
    // Preserve retry-all behavior when a hand-rolled composition omits a policy.
    private readonly IEventFaultPolicy _faultPolicy = faultPolicy ?? EventFaultPolicy.RetryAll;

    public async ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var config = options.Retry;
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
                var disposition = _faultPolicy.Decide(exception);
                if (disposition == FaultDisposition.Ignore)
                    return; // handled — the caller's success path acknowledges

                if (disposition == FaultDisposition.DeadLetter)
                    throw; // no redelivery would change the outcome — dead-letter now, budget untouched

                // Return the first delayed-retry fault so the caller can reschedule it off-slot.
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
