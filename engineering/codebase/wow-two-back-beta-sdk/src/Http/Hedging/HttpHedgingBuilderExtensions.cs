using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace WoW.Two.Sdk.Backend.Beta.Http.Hedging;

/// <summary>Applies the SDK's standard hedging pipeline to an outbound <see cref="IHttpClientBuilder"/>.</summary>
public static class HttpHedgingBuilderExtensions
{
    /// <summary>Adds the standard hedging handler (parallel attempt racing, per-endpoint circuit breaker, and timeouts) tuned from <see cref="HttpHedgingOptions"/>.</summary>
    /// <remarks>Alternative to <c>AddSdkResilience</c>: use hedging for latency-sensitive idempotent calls, sequential retry otherwise; don't stack both on one client.</remarks>
    /// <param name="builder">The HTTP client builder to extend.</param>
    /// <param name="configure">Optional override of the SDK hedging defaults.</param>
    public static IStandardHedgingHandlerBuilder AddSdkHedging(
        this IHttpClientBuilder builder,
        Action<HttpHedgingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new HttpHedgingOptions();
        configure?.Invoke(options);

        var hedging = builder.AddStandardHedgingHandler();
        hedging.Configure(o =>
        {
            o.Hedging.MaxHedgedAttempts = options.MaxHedgedAttempts;
            o.Hedging.Delay = options.HedgingDelay;
            o.TotalRequestTimeout.Timeout = options.TotalRequestTimeout;
            o.Endpoint.Timeout.Timeout = options.AttemptTimeout;
            o.Endpoint.CircuitBreaker.SamplingDuration = options.CircuitBreakerSamplingDuration;
            o.Endpoint.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
        });

        return hedging;
    }
}
