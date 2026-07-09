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

/// <summary>Polly-backed <see cref="IEventResiliencePipeline"/> — exponential-with-jitter retry, optional circuit breaker + timeout.</summary>
internal sealed class PollyEventResiliencePipeline : IEventResiliencePipeline
{
    private readonly ResiliencePipeline _pipeline;

    public PollyEventResiliencePipeline(PollyEventResilienceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var builder = new ResiliencePipelineBuilder();

        if (options.AttemptTimeout is { } timeout)
            builder.AddTimeout(timeout);

        builder.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = options.MaxRetryAttempts,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = options.BaseDelay,
        });

        if (options.UseCircuitBreaker)
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions());

        _pipeline = builder.Build();
    }

    public ValueTask ExecuteAsync(Func<CancellationToken, ValueTask> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _pipeline.ExecuteAsync(action, cancellationToken);
    }
}

/// <summary>DI registration for the Polly-backed event resilience pipeline.</summary>
public static class PollyEventResilienceServiceCollectionExtensions
{
    /// <summary>Replace the default <see cref="IEventResiliencePipeline"/> with a Polly-backed one (exp+jitter retry + optional breaker/timeout).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional pipeline configuration.</param>
    public static IServiceCollection AddPollyEventResilience(this IServiceCollection services, Action<PollyEventResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new PollyEventResilienceOptions();
        configure?.Invoke(options);
        services.Replace(ServiceDescriptor.Singleton<IEventResiliencePipeline>(new PollyEventResiliencePipeline(options)));
        return services;
    }
}
