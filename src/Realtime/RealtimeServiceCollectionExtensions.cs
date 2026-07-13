using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Realtime.SignalR;

namespace WoW.Two.Sdk.Backend.Beta.Realtime;

/// <summary>Registration helpers shared across the Realtime vector (SignalR presence, SSE, WebSockets).</summary>
public static class RealtimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory <see cref="IUserConnectionTracker"/> as a singleton for presence tracking.
    /// <see cref="SignalRConventions.AddConventionalSignalR"/> already does this; call it directly only when
    /// deriving from <see cref="PresenceHub"/> without the conventional SignalR registration. Idempotent.
    /// </summary>
    /// <param name="services">The service collection to add the tracker to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddUserConnectionTracking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IUserConnectionTracker, InMemoryUserConnectionTracker>();
        return services;
    }
}
