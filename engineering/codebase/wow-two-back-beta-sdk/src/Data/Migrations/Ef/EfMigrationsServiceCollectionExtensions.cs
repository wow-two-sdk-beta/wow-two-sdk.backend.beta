using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef;

/// <summary>Registration helpers for the EF Migrations runner.</summary>
public static class EfMigrationsServiceCollectionExtensions
{
    /// <summary>Registers an <see cref="EfMigrationsHostedService{TContext}"/> that runs <c>Database.MigrateAsync</c> at startup for <typeparamref name="TContext"/>.</summary>
    /// <typeparam name="TContext">The database context to migrate.</typeparam>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddEfMigrationsRunner<TContext>(this IServiceCollection services)
        where TContext : DbContext
        => services.AddEfMigrationsRunner<TContext>(static _ => { });

    /// <summary>Registers an <see cref="EfMigrationsHostedService{TContext}"/> with custom options.</summary>
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

        services.AddOptions<EfMigrationsOptions>()
            .Configure(configure)
            .ValidateOnStart();
        services.AddHostedService<EfMigrationsHostedService<TContext>>();
        return services;
    }
}
