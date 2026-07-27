using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.SqlServer;

/// <summary>SDK-conventional SqlServer provider helpers — retry-on-failure default + command timeout.</summary>
public static class SqlServerExtensions
{
    /// <summary>Configures SqlServer with SDK defaults: 6 retries on transient errors, 30-second command timeout.</summary>
    /// <param name="builder">The DbContext options builder to configure.</param>
    /// <param name="connectionString">The SqlServer connection string.</param>
    /// <param name="extra">An optional callback for further provider configuration.</param>
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
