using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;

/// <summary>Default <see cref="IEventResiliencePipeline"/> — retries via the configured <see cref="IRetryPolicy"/> + <see cref="RetryConfig"/>, then propagates the failure.</summary>
internal sealed class DefaultEventResiliencePipeline(
    IRetryPolicy retryPolicy,
    IOptions<InMemoryEventBusOptions> options,
    TimeProvider timeProvider) : IEventResiliencePipeline
{
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
            catch (Exception)
            {
                attempt++;
                var delay = retryPolicy.NextDelay(attempt, config);
                if (delay is null)
                    throw;

                await Task.Delay(delay.Value, timeProvider, cancellationToken);
            }
        }
    }
}
