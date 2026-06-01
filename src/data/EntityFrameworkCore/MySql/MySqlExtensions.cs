using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.MySql;

/// <summary>
/// SDK-conventional Pomelo MySQL provider helpers — retry-on-failure default + auto server-version.
/// </summary>
public static class MySqlExtensions
{
    /// <summary>
    /// Configures Pomelo MySQL with auto-detected server version and SDK retry defaults.
    /// </summary>
    public static DbContextOptionsBuilder UseMySqlConventional(
        this DbContextOptionsBuilder builder,
        string connectionString,
        Action<MySqlDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var version = ServerVersion.AutoDetect(connectionString);
        return builder.UseMySql(connectionString, version, mysql =>
        {
            mysql.EnableRetryOnFailure(maxRetryCount: 6);
            mysql.CommandTimeout(30);
            extra?.Invoke(mysql);
        });
    }

    /// <summary>
    /// Configures Pomelo MySQL with an explicit server version (useful when auto-detect should be avoided).
    /// </summary>
    public static DbContextOptionsBuilder UseMySqlConventional(
        this DbContextOptionsBuilder builder,
        string connectionString,
        ServerVersion serverVersion,
        Action<MySqlDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(serverVersion);

        return builder.UseMySql(connectionString, serverVersion, mysql =>
        {
            mysql.EnableRetryOnFailure(maxRetryCount: 6);
            mysql.CommandTimeout(30);
            extra?.Invoke(mysql);
        });
    }
}
