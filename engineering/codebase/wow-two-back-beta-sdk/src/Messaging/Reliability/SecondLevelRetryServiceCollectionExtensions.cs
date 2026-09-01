using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>DI registration for the second-level (retry → delay → dead-letter) tier model.</summary>
public static class SecondLevelRetryServiceCollectionExtensions
{
    /// <summary>
    /// Give an exhausted message a ladder of long delays before it is dead-lettered, so an outage that outlasts the
    /// fast retry budget does not fill the DLQ with messages that would have succeeded ten minutes later.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional tier ladder; the call itself is the opt-in. Defaults to <see cref="SecondLevelRetryOptions.DefaultTiers"/>.</param>
    /// <remarks>
    ///   - a promoted message reaches neither the consumed nor the dead-lettered counter, so alerting on dead-letter volume under-reports
    ///   - order against <c>AddConsumeInterceptor&lt;T&gt;()</c> decides where in the chain the promotion sits
    ///   - composes with <c>AddDelayedEventRetry()</c>, which moves the first level off the consume slot
    /// </remarks>
    public static IServiceCollection AddSecondLevelEventRetry(this IServiceCollection services, Action<SecondLevelRetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<SecondLevelRetryOptions>().Configure(options =>
        {
            options.Enabled = true;

        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<SecondLevelRetryOptions>>().Value);
            configure?.Invoke(options);
        });

        // Both are read to locate the end of the first-level budget; neither is necessarily registered by the caller.
        services.AddOptions<InMemoryEventBusOptions>();
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<InMemoryEventBusOptions>>().Value);
        services.AddOptions<DelayedRetryOptions>();
        services.TryAddSingleton(serviceProvider => serviceProvider.GetRequiredService<IOptions<DelayedRetryOptions>>().Value);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<SecondLevelRetryCoordinator>();
        services.AddSingleton<IConsumeInterceptor, RetryingConsumeInterceptor>();
        return services;
    }
}
