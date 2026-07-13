using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Caching.Core;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;

/// <summary>Registers HybridCache with conventional defaults and the <see cref="ICache"/> facade.</summary>
public static class HybridCachingServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="HybridCache"/> configured from <see cref="HybridCacheConventionOptions"/> and registers
    /// the <see cref="ICache"/> facade plus <see cref="ICacheKeyBuilder"/>. Pairs with
    /// <c>AddRedisDistributedCache</c> for an L2 tier — HybridCache picks up any registered
    /// <c>IDistributedCache</c> automatically; without one it runs L1-only.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional overrides for the conventional defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddHybridCaching(this IServiceCollection services, Action<HybridCacheConventionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var conventions = new HybridCacheConventionOptions();
        configure?.Invoke(conventions);

#pragma warning disable EXTEXP0018 // HybridCache experimental gate (no-op once GA)
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = conventions.DefaultExpiration,
                LocalCacheExpiration = conventions.DefaultLocalCacheExpiration,
            };
            options.MaximumPayloadBytes = conventions.MaximumPayloadBytes;
            options.MaximumKeyLength = conventions.MaximumKeyLength;
        });
#pragma warning restore EXTEXP0018

        services.TryAddSingleton<ICache, HybridCacheAdapter>();
        services.TryAddSingleton<ICacheKeyBuilder, CacheKeyBuilder>();
        return services;
    }
}
