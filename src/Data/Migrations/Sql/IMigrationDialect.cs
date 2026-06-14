using System.Data.Common;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Sql;

/// <summary>Defines the contract for provider-specific migration SQL: database creation, locking, and history DDL.</summary>
public interface IMigrationDialect
{
    /// <summary>Creates the target database if it is missing, returning whether a create occurred.</summary>
    /// <param name="connectionString">The connection string whose database name is ensured.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task<bool> EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct = default);

    /// <summary>Acquires the apply-loop advisory lock, blocking until it is granted.</summary>
    /// <param name="connection">The open connection that holds the lock for its session.</param>
    /// <param name="lockId">The advisory-lock id shared by every host.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task AcquireLockAsync(DbConnection connection, long lockId, CancellationToken ct = default);

    /// <summary>Releases the apply-loop advisory lock held by the session.</summary>
    /// <param name="connection">The connection that holds the lock.</param>
    /// <param name="lockId">The advisory-lock id to release.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task ReleaseLockAsync(DbConnection connection, long lockId, CancellationToken ct = default);

    /// <summary>Creates the migration-history table for the given schema and name if it does not exist.</summary>
    /// <param name="connection">The open connection to run the DDL on.</param>
    /// <param name="schemaName">The schema that owns the history table.</param>
    /// <param name="tableName">The history table name.</param>
    /// <param name="ct">Token to cancel the operation.</param>
    Task EnsureHistoryTableAsync(DbConnection connection, string schemaName, string tableName, CancellationToken ct = default);
}
