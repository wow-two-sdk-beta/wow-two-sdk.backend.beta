using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

/// <summary>Provides the SQLite migration dialect for file-create-on-open, busy-timeout serialization, and history-table DDL.</summary>
public sealed class SqliteMigrationDialect : IMigrationDialect
{
    private const int BusyTimeoutMilliseconds = 30_000;

    static SqliteMigrationDialect() => SqlMapper.AddTypeHandler(new DateTimeOffsetTextHandler());

    /// <inheritdoc />
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
    public Task AcquireLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) =>
        connection.ExecuteAsync(new CommandDefinition($"PRAGMA busy_timeout = {BusyTimeoutMilliseconds};", cancellationToken: ct));

    /// <inheritdoc />
    public Task ReleaseLockAsync(DbConnection connection, long lockId, CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
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

    /// <summary>Round-trips <see cref="DateTimeOffset"/> through SQLite TEXT as ISO 8601.</summary>
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
