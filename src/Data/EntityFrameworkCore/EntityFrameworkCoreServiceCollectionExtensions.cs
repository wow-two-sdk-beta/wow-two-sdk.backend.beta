using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;

/// <summary>Registration helpers for EF Core DbContexts using the SDK conventions.</summary>
public static class EntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>Registers a DbContext with SDK defaults (pooling on; dev logging auto by environment).</summary>
    /// <typeparam name="TContext">The concrete DbContext type. Must inherit <see cref="AppDbContextBase"/>.</typeparam>
    public static IServiceCollection AddEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
        where TContext : AppDbContextBase
        => services.AddEntityFrameworkCore<TContext>(static _ => { }, (_, builder) => configureProvider(builder));

    /// <summary>Registers a DbContext with SDK defaults and a service-provider-aware provider configurator.</summary>
    public static IServiceCollection AddEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureProvider)
        where TContext : AppDbContextBase
        => services.AddEntityFrameworkCore<TContext>(static _ => { }, configureProvider);

    /// <summary>Registers a DbContext with SDK defaults overridden via <see cref="EntityFrameworkCoreOptions"/>.</summary>
    public static IServiceCollection AddEntityFrameworkCore<TContext>(
        this IServiceCollection services,
        Action<EntityFrameworkCoreOptions> configureOptions,
        Action<DbContextOptionsBuilder> configureProvider)
        where TContext : AppDbContextBase
        => services.AddEntityFrameworkCore<TContext>(configureOptions, (_, builder) => configureProvider(builder));

    /// <summary>Registers a DbContext with SDK defaults overridden via options and a service-provider-aware provider configurator.</summary>
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

        void Apply(IServiceProvider serviceProvider, DbContextOptionsBuilder builder)
        {
            configureProvider(serviceProvider, builder);

            var isDevelopment = serviceProvider.GetService<IHostEnvironment>()?.IsDevelopment() ?? false;

            if (options.EnableSensitiveDataLogging ?? isDevelopment)
                builder.EnableSensitiveDataLogging();

            if (options.EnableDetailedErrors ?? isDevelopment)
                builder.EnableDetailedErrors();

            if (options.NoTrackingByDefault)
                builder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }

        if (options.UsePooling)
            services.AddDbContextPool<TContext>(Apply, options.PoolSize);
        else
            services.AddDbContext<TContext>(Apply);

        return services;
    }
}
