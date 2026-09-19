using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef;

/// <summary>Registration helpers for the EF Migrations runner.</summary>
public static class EfMigrationsServiceCollectionExtensions
{
    /// <summary>Registers an <see cref="EfMigrationsBackgroundService{TContext}"/> that runs <c>Database.MigrateAsync</c> at startup for <typeparamref name="TContext"/>.</summary>
    /// <typeparam name="TContext">The database context to migrate.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddEfMigrationsRunner<TContext>(this IServiceCollection services)
        where TContext : DbContext
        => services.AddEfMigrationsRunner<TContext>(static _ => { });

    /// <summary>Registers an <see cref="EfMigrationsBackgroundService{TContext}"/> with custom options.</summary>
    /// <typeparam name="TContext">The database context to migrate.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">A hook to configure the EF migrations runner options.</param>
    public static IServiceCollection AddEfMigrationsRunner<TContext>(
        this IServiceCollection services,
        Action<EfMigrationsOptions> configure)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<EfMigrationsOptions>(
            configure,
            builder => builder
                .Validate(options => options.MaxConnectAttempts > 0, "EfMigrationsOptions.MaxConnectAttempts must be positive.")
                .Validate(options => options.ConnectRetryDelay >= TimeSpan.Zero, "EfMigrationsOptions.ConnectRetryDelay must not be negative."));
        services.AddHostedService<EfMigrationsBackgroundService<TContext>>();
        return services;
    }
}
