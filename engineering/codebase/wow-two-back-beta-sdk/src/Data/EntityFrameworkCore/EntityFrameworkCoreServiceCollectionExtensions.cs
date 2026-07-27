using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Interceptors;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Registration helpers for EF Core DbContexts using the SDK conventions.</summary>
public static class EntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>Registers a DbContext with SDK defaults (pooling on; dev logging auto by environment).</summary>
    /// <typeparam name="TContext">The concrete DbContext type. Must inherit <see cref="AppDbContextBase"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureProvider">The callback that configures the database provider.</param>
    public static IServiceCollection AddEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
        where TContext : AppDbContextBase
        => services.AddEntityFrameworkCore<TContext>(static _ => { }, (_, builder) => configureProvider(builder));

    /// <summary>Registers a DbContext with SDK defaults and a service-provider-aware provider configurator.</summary>
    /// <typeparam name="TContext">The concrete DbContext type. Must inherit <see cref="AppDbContextBase"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureProvider">The callback that configures the database provider, with access to the service provider.</param>
    public static IServiceCollection AddEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureProvider)
        where TContext : AppDbContextBase
        => services.AddEntityFrameworkCore<TContext>(static _ => { }, configureProvider);

    /// <summary>Registers a DbContext with SDK defaults overridden via <see cref="EntityFrameworkCoreOptions"/>.</summary>
    /// <typeparam name="TContext">The concrete DbContext type. Must inherit <see cref="AppDbContextBase"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureOptions">The callback that overrides the SDK defaults.</param>
    /// <param name="configureProvider">The callback that configures the database provider.</param>
    public static IServiceCollection AddEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        Action<EntityFrameworkCoreOptions> configureOptions,
        Action<DbContextOptionsBuilder> configureProvider)
        where TContext : AppDbContextBase
        => services.AddEntityFrameworkCore<TContext>(configureOptions, (_, builder) => configureProvider(builder));

    /// <summary>Registers a DbContext with SDK defaults overridden via options and a service-provider-aware provider configurator.</summary>
    /// <typeparam name="TContext">The concrete DbContext type. Must inherit <see cref="AppDbContextBase"/>.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configureOptions">The callback that overrides the SDK defaults.</param>
    /// <param name="configureProvider">The callback that configures the database provider, with access to the service provider.</param>
    public static IServiceCollection AddEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        Action<EntityFrameworkCoreOptions> configureOptions,
        Action<IServiceProvider, DbContextOptionsBuilder> configureProvider)
        where TContext : AppDbContextBase
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);
        ArgumentNullException.ThrowIfNull(configureProvider);

        var options = new EntityFrameworkCoreOptions();
        configureOptions(options);

        // Record the context so the boot guard can verify every DI-registered interceptor actually landed on it.
        var registry = services.GetOrAddEfContextWiringRegistry();
        registry.Register(typeof(TContext));

        void Apply(IServiceProvider serviceProvider, DbContextOptionsBuilder builder)
            => EfInterceptorWiring.ApplySdkContextConfiguration(
                serviceProvider, builder, options, configureProvider, registry, typeof(TContext));

        if (options.UsePooling)
            services.AddDbContextPool<TContext>(Apply, options.PoolSize);
        else
            services.AddDbContext<TContext>(Apply);

        return services;
    }
}
