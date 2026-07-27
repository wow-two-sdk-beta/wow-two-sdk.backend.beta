using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Postgres;

/// <summary>SDK-conventional Npgsql provider helpers — retry-on-failure + migrations-assembly defaults.</summary>
public static class PostgresExtensions
{
    /// <summary>Configures Npgsql with SDK defaults (6 retries on transient errors, 30-second command timeout); pair with <c>UseSnakeCaseNamingConvention()</c> from the naming-conventions package.</summary>
    /// <param name="builder">The DbContext options builder to configure.</param>
    /// <param name="connectionString">The Postgres connection string.</param>
    /// <param name="extra">An optional callback for further Npgsql configuration.</param>
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

    /// <summary>Configures Npgsql using a shared <see cref="NpgsqlDataSource"/> — required when registering enum mappings at the driver level (Npgsql 9.x convention).</summary>
    /// <param name="builder">The DbContext options builder to configure.</param>
    /// <param name="dataSource">The shared Npgsql data source backing the connection.</param>
    /// <param name="extra">An optional callback for further Npgsql configuration.</param>
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
