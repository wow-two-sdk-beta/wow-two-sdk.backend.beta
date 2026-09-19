using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Policies;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Polly;

/// <summary>
/// Polly-backed <see cref="IEventResiliencePipeline"/> — exponential-with-jitter retry, optional circuit breaker +
/// timeout, gated by the same <see cref="IEventFaultPolicy"/> the default pipeline uses: a non-retryable failure
/// never enters Polly's retry loop, an ignored one is swallowed here.
/// </summary>
/// <remarks>
///   - under <see cref="DelayedRetryOptions"/> the retry strategy drops out, so <see cref="PollyEventResilienceOptions.MaxRetryAttempts"/> and <see cref="PollyEventResilienceOptions.BaseDelay"/> stop applying
///   - timeout and circuit breaker stay
///   - the retry schedule then comes from <see cref="DelayedRetryOptions.Retry"/>, or the shared <see cref="RetryConfig"/>
/// </remarks>
internal sealed class PollyEventResiliencePipeline : IEventResiliencePipeline
{
    private readonly ResiliencePipeline _pipeline;
    private readonly IEventFaultPolicy _faultPolicy;

    public PollyEventResiliencePipeline(PollyEventResilienceOptions options, IEventFaultPolicy faultPolicy, bool delayedRetryActive = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(faultPolicy);
        _faultPolicy = faultPolicy;

        var builder = new ResiliencePipelineBuilder();

        if (options.AttemptTimeout is { } timeout)
            builder.AddTimeout(timeout);

        if (!delayedRetryActive)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.BaseDelay,

                // Handle a Retry verdict only — a DeadLetter or Ignore verdict leaves the loop on the first failure.
                ShouldHandle = arguments => ValueTask.FromResult(ShouldRetry(arguments.Outcome.Exception)),
            });
        }

        if (options.UseCircuitBreaker)
        {
            // Same gate — a non-retryable failure must not count toward the breaker's failure ratio.
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = arguments => ValueTask.FromResult(ShouldRetry(arguments.Outcome.Exception)),
            });
        }

        _pipeline = builder.Build();
    }

    public async ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await _pipeline.ExecuteAsync(action, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (_faultPolicy.Decide(exception) == FaultDisposition.Ignore)
        {
            // Swallow so the caller's success path acknowledges; a DeadLetter verdict propagates to the caller.
        }
    }

    private bool ShouldRetry(Exception? exception)
        => exception is not null && _faultPolicy.Decide(exception) == FaultDisposition.Retry;
}
