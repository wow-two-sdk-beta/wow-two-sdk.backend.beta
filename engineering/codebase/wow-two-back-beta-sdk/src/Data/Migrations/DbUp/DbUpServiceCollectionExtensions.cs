using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp;

/// <summary>Registration helpers for the DbUp runner.</summary>
public static class DbUpServiceCollectionExtensions
{
    /// <summary>Registers the DbUp background service against <paramref name="connectionString"/>.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="connectionString">The connection string for the target database.</param>
    /// <param name="configure">A hook to configure the rest of the DbUp runner options.</param>
    public static IServiceCollection AddDbUpRunner(
        this IServiceCollection services,
        string connectionString,
        Action<DbUpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var dbUpOptions = new DbUpOptions { ConnectionString = connectionString };
        configure?.Invoke(dbUpOptions);
        services.TryAddSingleton(dbUpOptions);
        services.AddHostedService<DbUpBackgroundService>();
        return services;
    }
}
