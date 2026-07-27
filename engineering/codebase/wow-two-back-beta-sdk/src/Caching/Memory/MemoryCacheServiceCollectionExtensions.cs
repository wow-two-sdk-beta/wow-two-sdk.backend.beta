using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Memory;

/// <summary>Registers the in-process memory cache for trivial single-instance caching needs.</summary>
public static class MemoryCacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers <c>IMemoryCache</c>. For most scenarios prefer <c>AddHybridCaching</c> (which adds stampede
    /// protection, tags, and an optional distributed tier); use this only for simple in-process caching.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddInMemoryCaching(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMemoryCache();
        return services;
    }
}
