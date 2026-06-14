using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Http.Resilience;

/// <summary>
/// Applies the SDK's standard resilience pipeline to an outbound <see cref="IHttpClientBuilder"/>.
/// </summary>
public static class HttpResilienceBuilderExtensions
{
    /// <summary>
    /// Adds the standard resilience handler (retry + circuit breaker + per-attempt and total timeouts)
    /// tuned from <see cref="HttpResilienceOptions"/>. Layers on top of <c>Microsoft.Extensions.Http.Resilience</c>.
    /// </summary>
    /// <param name="builder">The HTTP client builder.</param>
    /// <param name="configure">Optional override of the SDK defaults.</param>
    public static IHttpClientBuilder AddSdkResilience(
        this IHttpClientBuilder builder,
        Action<HttpResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new HttpResilienceOptions();
        configure?.Invoke(options);

        builder.AddStandardResilienceHandler(handler =>
        {
            handler.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
            handler.AttemptTimeout.Timeout = options.AttemptTimeout;
            handler.TotalRequestTimeout.Timeout = options.TotalRequestTimeout;
            handler.CircuitBreaker.SamplingDuration = options.CircuitBreakerSamplingDuration;
            handler.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;
        });

        return builder;
    }
}
