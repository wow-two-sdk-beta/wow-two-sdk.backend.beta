using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Services;

namespace WoW.Two.Sdk.Backend.Beta.Messaging;

/// <summary>Provides registration for the messaging metrics seam.</summary>
public static class MessagingMetricsServiceCollectionExtensions
{
    /// <summary>
    /// Register the default <see cref="Meter"/>-backed <see cref="IMessagingMetricsService"/>. <c>TryAdd</c>-based, so a
    /// consumer registering their own implementation (or <see cref="NoOpMessagingMetricsService"/>) first keeps it.
    /// Every transport registration path calls this; calling it directly is only needed for a hand-rolled composition.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddMessagingMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IMessagingMetricsService, DefaultMessagingMetricsService>();
        return services;
    }
}
