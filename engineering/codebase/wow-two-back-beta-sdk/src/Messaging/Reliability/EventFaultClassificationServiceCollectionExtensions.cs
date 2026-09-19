using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Policies;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>DI registration for consume-side exception classification.</summary>
public static class EventFaultClassificationServiceCollectionExtensions
{
    /// <summary>
    /// Configure which exceptions skip the retry budget. Applies to every <see cref="IEventResiliencePipeline"/>
    /// implementation (default and Polly-backed), so classification never depends on which one is registered.
    /// Call order relative to the transport/resilience registrations does not matter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The classification rules, evaluated first-match-wins.</param>
    public static IServiceCollection AddEventFaultClassification(this IServiceCollection services, Action<EventFaultClassificationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new EventFaultClassificationOptions();
        configure(options);
        services.Replace(ServiceDescriptor.Singleton<IEventFaultPolicy>(new EventFaultPolicy(options)));
        return services;
    }

    /// <summary>Replace the rule-based policy with a custom <see cref="IEventFaultPolicy"/> implementation.</summary>
    /// <typeparam name="TPolicy">The policy implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddEventFaultPolicy<TPolicy>(this IServiceCollection services)
        where TPolicy : class, IEventFaultPolicy
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IEventFaultPolicy, TPolicy>());
        return services;
    }
}
