using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.Postgres;

/// <summary>PostgreSQL storage preset for Hangfire jobs.</summary>
public static class PostgresHangfireServiceCollectionExtensions
{
    /// <summary>Registers the Hangfire client and server on PostgreSQL storage (the <c>hangfire</c> schema is auto-created on first run).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="configure">Optional worker-count / queue / polling tuning.</param>
    /// <param name="configureStorage">Optional storage tuning — queue poll interval, schema, invisibility timeout.</param>
    public static IServiceCollection AddPostgresHangfireJobs(
        this IServiceCollection services,
        string connectionString,
        Action<HangfireJobsOptions>? configure = null,
        Action<PostgreSqlStorageOptions>? configureStorage = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return services.AddHangfireJobs(
            config => config.UsePostgreSqlStorage(
                storage => storage.UseNpgsqlConnection(connectionString),
                CreateStorageOptions(configureStorage)),
            configure);
    }

    /// <summary>
    /// Registers the Hangfire client and server on the database the persistence floor registered
    /// (<c>AddPostgresPersistence</c>) — its <see cref="DatabaseSettings"/> connection, resolved when the job storage is
    /// first built, so a test host's connection override reaches it too.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional worker-count / queue / polling tuning.</param>
    /// <param name="configureStorage">Optional storage tuning — queue poll interval, schema, invisibility timeout.</param>
    public static IServiceCollection AddPostgresHangfireJobs(
        this IServiceCollection services,
        Action<HangfireJobsOptions>? configure = null,
        Action<PostgreSqlStorageOptions>? configureStorage = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHangfireJobs(
            (provider, config) => config.UsePostgresPersistenceStorage(provider, configureStorage),
            configure);
    }

    /// <summary>
    /// Points Hangfire at the persistence floor's database — for a host that picks its job store at startup
    /// (<c>AddHangfireJobs((provider, config) => …)</c>) and chooses PostgreSQL.
    /// </summary>
    /// <param name="config">The Hangfire configuration being built.</param>
    /// <param name="provider">The built service provider holding the registered <see cref="DatabaseSettings"/>.</param>
    /// <param name="configureStorage">Optional storage tuning — queue poll interval, schema, invisibility timeout.</param>
    /// <returns>The same configuration for chaining.</returns>
    public static IGlobalConfiguration UsePostgresPersistenceStorage(
        this IGlobalConfiguration config,
        IServiceProvider provider,
        Action<PostgreSqlStorageOptions>? configureStorage = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(provider);

        var connectionString = provider.GetRequiredService<DatabaseSettings>().ConnectionString;
        config.UsePostgreSqlStorage(
            storage => storage.UseNpgsqlConnection(connectionString),
            CreateStorageOptions(configureStorage));
        return config;
    }

    /// <summary>Creates the storage options with the caller's tuning applied.</summary>
    /// <param name="configureStorage">The optional tuning.</param>
    /// <returns>The storage options.</returns>
    private static PostgreSqlStorageOptions CreateStorageOptions(Action<PostgreSqlStorageOptions>? configureStorage)
    {
        var options = new PostgreSqlStorageOptions();
        configureStorage?.Invoke(options);
        return options;
    }
}
