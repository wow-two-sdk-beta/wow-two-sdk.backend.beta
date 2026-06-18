using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides the SQLite dialect: file-create-on-open, busy-timeout serialization, and history-table DDL.</summary>
/// <remarks>
/// Use as the <see cref="IMigrationDialect"/> when <see cref="MigrationOptions.Provider"/> is <see cref="DatabaseProvider.Sqlite"/>.
/// SQLite has no advisory lock and no user schemas, so locking maps to <c>PRAGMA busy_timeout</c> and the schema name is ignored.
/// </remarks>
public sealed class SqliteMigrationDialect : IMigrationDialect
{
    private const int BusyTimeoutMilliseconds = 30_000;

    // SQLite stores DateTimeOffset as TEXT; Dapper does not convert TEXT to DateTimeOffset natively (Npgsql returns it
    // typed, SQLite does not). Register the round-trip handler once — only a SQLite-using process ever touches this type.
    static SqliteMigrationDialect() => SqlMapper.AddTypeHandler(new DateTimeOffsetTextHandler());

    /// <inheritdoc />
    /// <remarks>SQLite creates the database file lazily on first open; this only ensures the parent directory exists and reports whether the file was absent beforehand. In-memory databases report no create.</remarks>
    public Task<bool> EnsureDatabaseExistsAsync(string connectionString, CancellationToken ct = default)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        var dataSource = builder.DataSource;
        if (string.IsNullOrWhiteSpace(dataSource)
            || builder.Mode == SqliteOpenMode.Memory
            || string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        var fullPath = Path.GetFullPath(dataSource);
        var existedBefore = File.Exists(fullPath);

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        return Task.FromResult(!existedBefore);
    }

    /// <inheritdoc />
    /// <remarks>SQLite has no advisory lock; <c>busy_timeout</c> makes a concurrent writer wait on the file lock instead of failing with <c>SQLITE_BUSY</c>. The <paramref name="lockId"/> is ignored.</remarks>
    public Task AcquireLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) =>
        connection.ExecuteAsync(new CommandDefinition($"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};", cancellationToken: ct));

    /// <inheritdoc />
    /// <remarks>No-op — SQLite holds no advisory lock; the write lock releases when the transaction ends.</remarks>
    public Task ReleaseLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>SQLite has no user schemas; <paramref name="schemaName"/> is ignored and the table is quoted unqualified.</remarks>
    public Task EnsureHistoryTableAsync(DbConnection connection, string schemaName, string tableName, CancellationToken ct = default)
    {
        var sql = $"""
            CREATE TABLE IF NOT EXISTS {QualifyHistoryTable(schemaName, tableName)} (
                ordinal       INTEGER NOT NULL,
                version       TEXT    NOT NULL,
                name          TEXT    NOT NULL,
                checksum      TEXT    NOT NULL,
                applied_at    TEXT    NOT NULL,
                applied_by    TEXT    NOT NULL,
                execution_ms  INTEGER NOT NULL,
                CONSTRAINT pk_{tableName} PRIMARY KEY (ordinal)
            );
            """;
        return connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }

    /// <inheritdoc />
    public string QualifyHistoryTable(string schemaName, string tableName) => $"\"{tableName}\"";

    /// <summary>Round-trips <see cref="DateTimeOffset"/> through SQLite TEXT as ISO 8601, which Dapper does not convert natively.</summary>
    private sealed class DateTimeOffsetTextHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        /// <inheritdoc />
        public override DateTimeOffset Parse(object value) => value switch
        {
            DateTimeOffset offset => offset,
            DateTime dateTime => new DateTimeOffset(dateTime, TimeSpan.Zero),
            string text => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType().Name ?? "null"} to DateTimeOffset."),
        };

        /// <inheritdoc />
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
            parameter.Value = value.ToString("O", CultureInfo.InvariantCulture);
    }
}
