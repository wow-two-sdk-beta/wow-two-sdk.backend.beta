using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Policies;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Services;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Polly;

/// <summary>DI registration for the Polly-backed event resilience pipeline.</summary>
public static class PollyEventResilienceServiceCollectionExtensions
{
    /// <summary>Replace the default <see cref="IEventResiliencePipeline"/> with a Polly-backed one (exp+jitter retry + optional breaker/timeout).</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional pipeline configuration.</param>
    /// <remarks>
    ///   - exception classification is shared with the default pipeline via <c>AddEventFaultClassification</c>
    ///   - with no faultPolicy registered every exception classifies as <see cref="FaultDisposition.Retry"/>
    ///   - call order against <c>AddEventFaultClassification</c> and <c>AddDelayedEventRetry</c> does not matter
    /// </remarks>
    public static IServiceCollection AddPollyEventResilience(this IServiceCollection services, Action<PollyEventResilienceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new PollyEventResilienceOptions();
        configure?.Invoke(options);
        services.Replace(ServiceDescriptor.Singleton<IEventResiliencePipeline>(provider =>
            new PollyEventResiliencePipeline(
                options,
                provider.GetService<IEventFaultPolicy>() ?? EventFaultPolicy.RetryAll,
                provider.GetService<DelayedRetryService>() is { IsActive: true })));
        return services;
    }
}
