using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Polly;

/// <summary>Options for the Polly-backed event resilience pipeline.</summary>
public sealed class PollyEventResilienceOptions
{
    /// <summary>Maximum retry attempts before the failure propagates (→ dead-letter). Default 5.</summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>Base delay for the exponential-with-jitter backoff. Default 200ms.</summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Add a circuit breaker that trips on a sustained failure ratio. Default off.</summary>
    public bool UseCircuitBreaker { get; set; }

    /// <summary>Optional per-attempt timeout. Default none.</summary>
    public TimeSpan? AttemptTimeout { get; set; }
}

/// <summary>
/// Polly-backed <see cref="IEventResiliencePipeline"/> — exponential-with-jitter retry, optional circuit breaker +
/// timeout, gated by the same <see cref="IEventFaultClassifier"/> the default pipeline uses: a non-retryable failure
/// never enters Polly's retry loop, an ignored one is swallowed here.
/// </summary>
/// <remarks>
/// Under <see cref="DelayedRetryOptions"/> the retry strategy is left out of the pipeline entirely — the wait belongs
/// on the re-enqueue path, not in the consume slot — while the timeout and circuit breaker stay, since neither holds a
/// message through a backoff. The retry <i>schedule</i> then comes from <see cref="DelayedRetryOptions.Retry"/> (or the
/// shared <see cref="RetryConfig"/>): <see cref="PollyEventResilienceOptions.MaxRetryAttempts"/> and
/// <see cref="PollyEventResilienceOptions.BaseDelay"/> describe a loop that is no longer running.
/// </remarks>
internal sealed class PollyEventResiliencePipeline : IEventResiliencePipeline
{
    private readonly ResiliencePipeline _pipeline;
    private readonly IEventFaultClassifier _classifier;

    public PollyEventResiliencePipeline(PollyEventResilienceOptions options, IEventFaultClassifier classifier, bool delayedRetryActive = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(classifier);
        _classifier = classifier;

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

                // Replaces Polly's default "handle every exception": only a Retry verdict is handled, so a DeadLetter or
                // Ignore verdict leaves the loop unhandled and surfaces to ExecuteAsync on the first failure. With no rules
                // configured every exception classifies as Retry, which is the default predicate's behaviour exactly.
                ShouldHandle = arguments => ValueTask.FromResult(ShouldRetry(arguments.Outcome.Exception)),
            });
        }

        if (options.UseCircuitBreaker)
        {
            // Same gate: a non-retryable failure is a property of the message, not of a failing downstream, so it must
            // not count toward the breaker's failure ratio and stall every other message.
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
        catch (Exception exception) when (_classifier.Classify(exception) == FaultDisposition.Ignore)
        {
            // Handled — swallow so the caller's success path acknowledges. A DeadLetter verdict is deliberately not
            // caught: it propagates, and the caller dead-letters it.
        }
    }

    private bool ShouldRetry(Exception? exception)
        => exception is not null && _classifier.Classify(exception) == FaultDisposition.Retry;
}

/// <summary>DI registration for the Polly-backed event resilience pipeline.</summary>
public static class PollyEventResilienceServiceCollectionExtensions
{
    /// <summary>Replace the default <see cref="IEventResiliencePipeline"/> with a Polly-backed one (exp+jitter retry + optional breaker/timeout).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional pipeline configuration.</param>
    /// <remarks>
    /// Exception classification is <b>not</b> configured here — it is shared with the default pipeline via
    /// <c>AddEventFaultClassification</c>. Registered as a factory rather than an instance so the classifier is
    /// resolved when the pipeline is first used, which makes the two calls order-independent. The same lateness lets
    /// <c>AddDelayedEventRetry</c> be called on either side of this one and still drop the retry strategy.
    /// </remarks>
    public static IServiceCollection AddPollyEventResilience(this IServiceCollection services, Action<PollyEventResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new PollyEventResilienceOptions();
        configure?.Invoke(options);
        services.Replace(ServiceDescriptor.Singleton<IEventResiliencePipeline>(provider =>
            new PollyEventResiliencePipeline(
                options,
                provider.GetService<IEventFaultClassifier>() ?? DefaultEventFaultClassifier.RetryAll,
                provider.GetService<DelayedRetryCoordinator>() is { IsActive: true })));
        return services;
    }
}
