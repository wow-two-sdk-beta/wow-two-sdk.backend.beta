using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Geo.Distance;

namespace WoW.Two.Sdk.Backend.Beta.Geo;

/// <summary>Registration helpers for the Geo vector.</summary>
public static class GeoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the injectable Geo services as singletons — currently
    /// <see cref="IGeoDistanceCalculator"/> (haversine). Coordinates, geohash, and GeoJSON are static/value
    /// types used directly. The call is idempotent.
    /// </summary>
    /// <param name="services">The service collection to add the Geo services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddGeo(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IGeoDistanceCalculator, GeoDistanceCalculator>();
        return services;
    }
}
