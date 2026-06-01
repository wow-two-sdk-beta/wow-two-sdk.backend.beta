using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Ef;

/// <summary>
/// Registration helpers for the EF Migrations runner.
/// </summary>
public static class EfMigrationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="EfMigrationsHostedService{TContext}"/> that runs
    /// <c>Database.MigrateAsync</c> at startup for <typeparamref name="TContext"/>.
    /// </summary>
    public static IServiceCollection AddEfMigrationsRunner<TContext>(this IServiceCollection services)
        where TContext : DbContext
        => services.AddEfMigrationsRunner<TContext>(static _ => { });

    /// <summary>
    /// Registers an <see cref="EfMigrationsHostedService{TContext}"/> with custom options.
    /// </summary>
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
