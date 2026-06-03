using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite;

/// <summary>
/// SDK-conventional Sqlite provider helpers — 30-second command timeout default.
/// </summary>
/// <remarks>
/// Sqlite does not need retry-on-failure (no transient network errors). Foreign key
/// enforcement is on by default in EF Core's Sqlite provider since 5.0.
/// </remarks>
public static class SqliteExtensions
{
    /// <summary>
    /// Configures Sqlite with a 30-second command timeout default.
    /// </summary>
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
