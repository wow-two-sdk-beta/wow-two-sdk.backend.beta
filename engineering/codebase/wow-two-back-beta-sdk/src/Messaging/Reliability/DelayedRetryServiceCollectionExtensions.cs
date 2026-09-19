using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Services;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>DI registration for re-enqueue-with-delay retry.</summary>
public static class DelayedRetryServiceCollectionExtensions
{
    /// <summary>
    /// Retry a failed message by re-publishing it with a delay instead of waiting in the consume slot, so the backoff
    /// stops pinning a consumer (a Kafka consume loop, a RabbitMQ prefetch slot, a concurrency-pump worker).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional overrides — chiefly a retry schedule of its own; the call itself is the opt-in.</param>
    /// <remarks>
    ///   - order-independent relative to the transport and resilience registrations
    ///   - needs a delay-capable transport and an <see cref="IDelayedDeliveryService"/>, or the in-process delay stays
    ///   - the downgrade logs once at startup
    /// </remarks>
    public static IServiceCollection AddDelayedEventRetry(this IServiceCollection services, Action<DelayedRetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The call is the opt-in; a bound configuration section can still switch it back off.
        services.AddValidatedOptions<DelayedRetryOptions>(
            options =>
            {
                options.Enabled = true;
                configure?.Invoke(options);
            },
            builder => builder
                .Validate(options => options.Retry is null || options.Retry.MaxAttempts > 0, "DelayedRetryOptions.Retry.MaxAttempts must be positive.")
                .Validate(options => options.Retry is null || Enum.IsDefined(options.Retry.Backoff), "DelayedRetryOptions.Retry.Backoff must be a defined backoff kind.")
                .Validate(options => options.Retry?.BaseDelay is null || options.Retry.BaseDelay >= TimeSpan.Zero, "DelayedRetryOptions.Retry.BaseDelay must not be negative.")
                .Validate(options => options.Retry?.MaxDelay is null || options.Retry.MaxDelay >= TimeSpan.Zero, "DelayedRetryOptions.Retry.MaxDelay must not be negative.")
                .Validate(options => options.Retry?.BaseDelay is null || options.Retry.MaxDelay is null || options.Retry.MaxDelay >= options.Retry.BaseDelay, "DelayedRetryOptions.Retry.MaxDelay must be at least BaseDelay."));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRetryPolicy, RetryPolicy>();
        services.TryAddSingleton<DelayedRetryService>();
        return services;
    }
}
