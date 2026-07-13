using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Redis;

/// <summary>Registers a Redis distributed cache to serve as HybridCache's L2 (shared) tier.</summary>
public static class RedisCacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers a StackExchange.Redis-backed <c>IDistributedCache</c>. When <c>AddHybridCaching</c> is also
    /// called, HybridCache automatically uses this as its L2 tier, giving cross-instance cache sharing.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="connectionString">The Redis connection string (StackExchange.Redis format).</param>
    /// <param name="instanceName">Optional key prefix isolating this app's entries within a shared Redis.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddRedisDistributedCache(this IServiceCollection services, string connectionString, string? instanceName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            if (!string.IsNullOrWhiteSpace(instanceName))
                options.InstanceName = instanceName;
        });

        return services;
    }
}
