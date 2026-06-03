using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;

/// <summary>
/// SDK-conventional Npgsql provider helpers — retry-on-failure + migrations-assembly defaults.
/// </summary>
public static class PostgresExtensions
{
    /// <summary>
    /// Configures Npgsql with SDK defaults: 6 retries on transient errors, 30-second command timeout.
    /// Pair with <c>UseSnakeCaseNamingConvention()</c> from the naming-conventions package.
    /// </summary>
    public static DbContextOptionsBuilder UseNpgsqlConventional(
        this DbContextOptionsBuilder builder,
        string connectionString,
        Action<NpgsqlDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.EnableRetryOnFailure(maxRetryCount: 6);
            npgsql.CommandTimeout(30);
            extra?.Invoke(npgsql);
        });
    }

    /// <summary>
    /// Configures Npgsql using a shared <see cref="NpgsqlDataSource"/> — required when registering
    /// enum mappings at the driver level (Npgsql 9.x convention).
    /// </summary>
    public static DbContextOptionsBuilder UseNpgsqlConventional(
        this DbContextOptionsBuilder builder,
        NpgsqlDataSource dataSource,
        Action<NpgsqlDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataSource);

        return builder.UseNpgsql(dataSource, npgsql =>
        {
            npgsql.EnableRetryOnFailure(maxRetryCount: 6);
            npgsql.CommandTimeout(30);
            extra?.Invoke(npgsql);
        });
    }
}
