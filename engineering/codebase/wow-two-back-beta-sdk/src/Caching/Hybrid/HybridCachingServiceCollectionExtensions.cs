using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Caching.Core;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Hybrid;

/// <summary>Registers HybridCache with conventional defaults and the <see cref="ICacheRepository"/> facade.</summary>
public static class HybridCachingServiceCollectionExtensions
{
    /// <summary>
    /// Adds <see cref="HybridCache"/> configured from <see cref="HybridCacheConventionOptions"/> (code, then the host
    /// section <c>Caching:Hybrid</c>) with the <see cref="JsonCacheSerializerFactory"/>, and registers the
    /// <see cref="ICacheRepository"/> facade plus <see cref="ICacheKeyBuilder"/>. Pairs with
    /// <c>AddRedisDistributedCache</c> for an L2 tier — HybridCache picks up any registered
    /// <c>IDistributedCache</c> automatically; without one it runs L1-only.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional overrides for the conventional defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddHybridCaching(this IServiceCollection services, Action<HybridCacheConventionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            HybridCacheConventionOptions.SectionName,
            configure,
            builder => builder
                .Validate(o => o.DefaultExpiration > TimeSpan.Zero && o.DefaultLocalCacheExpiration > TimeSpan.Zero, "HybridCacheConventionOptions expirations must be positive.")
                .Validate(o => o.MaximumPayloadBytes > 0 && o.MaximumKeyLength > 0, "HybridCacheConventionOptions size limits must be positive.")
                .Validate(o => Enum.IsDefined(o.DeserializationFailure), "HybridCacheConventionOptions.DeserializationFailure is unknown."));
#pragma warning disable EXTEXP0018 // HybridCache experimental gate (no-op once GA)
        services.AddHybridCache().AddSerializerFactory<JsonCacheSerializerFactory>();
#pragma warning restore EXTEXP0018
        services.AddOptions<HybridCacheOptions>().Configure<IOptions<HybridCacheConventionOptions>>((options, conventions) =>
        {
            var current = conventions.Value;
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = current.DefaultExpiration,
                LocalCacheExpiration = current.DefaultLocalCacheExpiration,
            };
            options.MaximumPayloadBytes = current.MaximumPayloadBytes;
            options.MaximumKeyLength = current.MaximumKeyLength;
        });
        services.TryAddSingleton<ICacheRepository, HybridCacheRepository>();
        services.TryAddSingleton<ICacheKeyBuilder, CacheKeyBuilder>();
        return services;
    }
}
