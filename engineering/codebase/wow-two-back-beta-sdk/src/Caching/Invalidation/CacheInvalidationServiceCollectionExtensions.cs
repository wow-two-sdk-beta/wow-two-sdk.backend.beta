using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Caching.Invalidation.BackgroundServices;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation;

/// <summary>Provides registration for cross-host cache invalidation over PostgreSQL notifications.</summary>
public static class CacheInvalidationServiceCollectionExtensions
{
    /// <summary>Registers the listener that evicts local cache entries when any host commits an invalidation.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Sets the connection string, channel and reconnect delay.</param>
    /// <remarks>
    ///   - the default handler evicts through <c>ICacheRepository</c>; register another <see cref="ICacheInvalidationHandler"/> first to replace it
    ///   - give cached entries a lifetime: it bounds staleness if a notification is ever missed
    /// </remarks>
    public static IServiceCollection AddPostgresCacheInvalidation(
        this IServiceCollection services,
        Action<PostgresCacheInvalidationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions(
            configure,
            static builder => builder
                .Validate(static options => !string.IsNullOrWhiteSpace(options.ConnectionString), "CacheInvalidation: ConnectionString is required.")
                .Validate(static options => !string.IsNullOrWhiteSpace(options.Channel), "CacheInvalidation: Channel is required.")
                .Validate(static options => options.ReconnectDelay > TimeSpan.Zero, "CacheInvalidation: ReconnectDelay must be positive."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICacheInvalidationHandler, CacheRepositoryInvalidationHandler>();
        services.AddHostedService<PostgresCacheInvalidationBackgroundService>();
        return services;
    }

    /// <summary>Evicts invalidated entries from the host's <c>IMemoryCache</c> instead of <c>ICacheRepository</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <remarks>
    ///   - for hosts that cache live objects in memory, where serializing through HybridCache costs more than it saves
    ///   - call before or after <see cref="AddPostgresCacheInvalidation"/>; it replaces the default handler either way
    /// </remarks>
    public static IServiceCollection AddMemoryCacheInvalidationHandler(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        services.Replace(ServiceDescriptor.Singleton<ICacheInvalidationHandler, MemoryCacheInvalidationHandler>());
        return services;
    }
}
