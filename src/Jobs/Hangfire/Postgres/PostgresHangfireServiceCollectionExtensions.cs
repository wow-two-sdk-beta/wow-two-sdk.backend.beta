using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Hangfire.Postgres;

/// <summary>PostgreSQL storage preset for Hangfire jobs.</summary>
public static class PostgresHangfireServiceCollectionExtensions
{
    /// <summary>
    /// Registers Hangfire client + server on PostgreSQL storage (schema auto-created in
    /// the <c>hangfire</c> schema on first run).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="configure">Optional worker-count / queue tuning.</param>
    public static IServiceCollection AddPostgresHangfireJobs(
        this IServiceCollection services,
        string connectionString,
        Action<HangfireJobsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return services.AddHangfireJobs(
            config => config.UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(connectionString)),
            configure);
    }
}
