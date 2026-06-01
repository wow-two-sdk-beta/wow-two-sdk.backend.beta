using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SqlServer;

/// <summary>
/// SDK-conventional SqlServer provider helpers — retry-on-failure default + command timeout.
/// </summary>
public static class SqlServerExtensions
{
    /// <summary>
    /// Configures SqlServer with SDK defaults: 6 retries on transient errors, 30-second command timeout.
    /// </summary>
    public static DbContextOptionsBuilder UseSqlServerConventional(
        this DbContextOptionsBuilder builder,
        string connectionString,
        Action<SqlServerDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(maxRetryCount: 6);
            sql.CommandTimeout(30);
            extra?.Invoke(sql);
        });
    }
}
