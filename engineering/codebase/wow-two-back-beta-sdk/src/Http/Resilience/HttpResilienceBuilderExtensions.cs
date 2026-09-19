using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using WoW.Two.Sdk.Backend.Beta.Http.Resilience.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Http.Resilience;

/// <summary>Applies the SDK's standard resilience pipeline to an outbound <see cref="IHttpClientBuilder"/>.</summary>
public static class HttpResilienceBuilderExtensions
{
    /// <summary>Adds the standard resilience handler (retry, circuit breaker, and timeouts) tuned from <see cref="HttpResilienceOptions"/>, over <c>Microsoft.Extensions.Http.Resilience</c>.</summary>
    /// <param name="builder">The HTTP client builder to extend.</param>
    /// <param name="configure">Optional override of the SDK resilience defaults.</param>
    public static IHttpClientBuilder AddSdkResilience(
        this IHttpClientBuilder builder,
        Action<HttpResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new HttpResilienceOptions();
        configure?.Invoke(options);
        options.Validate();

        builder.AddStandardResilienceHandler(handler =>
        {
            handler.Retry.MaxRetryAttempts = options.MaxRetryAttempts;
            handler.Retry.Delay = options.RetryDelay;
            handler.AttemptTimeout.Timeout = options.AttemptTimeout;
            handler.TotalRequestTimeout.Timeout = options.TotalRequestTimeout;
            handler.CircuitBreaker.SamplingDuration = options.CircuitBreakerSamplingDuration;
            handler.CircuitBreaker.FailureRatio = options.CircuitBreakerFailureRatio;

            var shouldHandleTransient = handler.Retry.ShouldHandle;
            handler.Retry.ShouldHandle = async arguments =>
            {
                if (arguments.Context.CancellationToken.IsCancellationRequested)
                    return false;

                var request = arguments.Context.GetRequestMessage()
                              ?? arguments.Outcome.Result?.RequestMessage;
                if (request is null
                    || !request.IsReplaySafe(options.UnsafeRequestReplaySelector))
                    return false;

                return await shouldHandleTransient(arguments).ConfigureAwait(false);
            };
        });

        return builder;
    }
}
