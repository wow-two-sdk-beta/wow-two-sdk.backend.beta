using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

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
        services.AddOptions<DelayedRetryOptions>().Configure(options =>
        {
            options.Enabled = true;

        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<DelayedRetryOptions>>().Value);
            configure?.Invoke(options);
        });

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRetryPolicy, DefaultRetryPolicy>();
        services.TryAddSingleton<DelayedRetryCoordinator>();
        return services;
    }
}
