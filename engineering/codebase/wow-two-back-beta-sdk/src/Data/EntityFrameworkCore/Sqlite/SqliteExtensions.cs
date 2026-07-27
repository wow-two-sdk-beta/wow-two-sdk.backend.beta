using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite;

/// <summary>SDK-conventional Sqlite provider helpers — 30-second command timeout default.</summary>
/// <remarks>Sqlite needs no retry-on-failure (no transient network errors); foreign-key enforcement is on by default in EF Core's Sqlite provider since 5.0.</remarks>
public static class SqliteExtensions
{
    /// <summary>Configures Sqlite with a 30-second command timeout default.</summary>
    /// <param name="builder">The DbContext options builder to configure.</param>
    /// <param name="connectionString">The Sqlite connection string.</param>
    /// <param name="extra">An optional callback for further provider configuration.</param>
    public static DbContextOptionsBuilder UseSqliteConventional(
        this DbContextOptionsBuilder builder,
        string connectionString,
        Action<SqliteDbContextOptionsBuilder>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        return builder.UseSqlite(connectionString, sqlite =>
        {
            sqlite.CommandTimeout(30);
            extra?.Invoke(sqlite);
        });
    }
}
