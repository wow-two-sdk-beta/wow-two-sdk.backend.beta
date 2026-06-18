using System.Data.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Migrations.Tests.Harness;

/// <summary>
/// One configured migrator over a temp <see cref="MigrationsWorkspace"/> and a SQLite file DB — the DI graph the
/// engine runs inside, wired exactly as a SQLite host would (<c>AddSqliteConnectionFactory</c> +
/// <c>AddDatabaseBespokeMigrations</c> with <see cref="DatabaseProvider.Sqlite"/>).
/// </summary>
public sealed class SqliteMigrator : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private SqliteMigrator(ServiceProvider provider) => _provider = provider;

    /// <summary>The host-facing runner under test.</summary>
    public IMigrationRunnerService Runner => _provider.GetRequiredService<IMigrationRunnerService>();

    /// <summary>The effective options (after the configure hook).</summary>
    public MigrationOptions Options => _provider.GetRequiredService<MigrationOptions>();

    /// <summary>Builds a migrator over <paramref name="connectionString"/> reading from <paramref name="root"/>.</summary>
    public static SqliteMigrator Create(string connectionString, string root, Action<MigrationOptions>? configure = null)
    {
        // The engine aliases history columns, but keep parity with the product's Dapper convention.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddSimpleConsole());

        // SQLite has no DbDataSource, so the dedicated SQLite factory is the seam (the production adoption path).
        services.AddSqliteConnectionFactory(connectionString);

        services.AddDatabaseBespokeMigrations(root, o =>
        {
            o.Provider = DatabaseProvider.Sqlite;
            configure?.Invoke(o);
        });

        return new SqliteMigrator(services.BuildServiceProvider());
    }

    /// <summary>Opens a connection via the SDK factory (the same seam the runner uses) for direct DB assertions.</summary>
    public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken ct = default) =>
        _provider.GetRequiredService<IDbConnectionFactory>().CreateOpenAsync(ct);

    /// <summary>Reads every <c>migration_history</c> row ordered by ordinal — the source of truth for recording assertions.</summary>
    public async Task<IReadOnlyList<HistoryRow>> ReadHistoryAsync(CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<HistoryRow>(
            "select ordinal, name, checksum, applied_by as AppliedBy from migration_history order by ordinal");
        return rows.AsList();
    }

    /// <summary>True when a table exists in the SQLite catalog — used to assert a table got created / dropped.</summary>
    public async Task<bool> TableExistsAsync(string table, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        var count = await conn.ExecuteScalarAsync<long>(
            "select count(*) from sqlite_master where type = 'table' and name = @table", new { table });
        return count > 0;
    }

    /// <summary>True when an index exists in the SQLite catalog — used to assert a @no-transaction index landed.</summary>
    public async Task<bool> IndexExistsAsync(string index, CancellationToken ct = default)
    {
        await using var conn = await OpenConnectionAsync(ct);
        var count = await conn.ExecuteScalarAsync<long>(
            "select count(*) from sqlite_master where type = 'index' and name = @index", new { index });
        return count > 0;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
}

/// <summary>A projected <c>migration_history</c> row for test assertions. A settable-property class (not a record)
/// so Dapper's property path leniently converts SQLite's <c>Int64</c> ordinal to <c>int</c> — record ctor-matching
/// is strict on the column type and would reject it.</summary>
public sealed class HistoryRow
{
    /// <summary>The migration ordinal.</summary>
    public int Ordinal { get; set; }

    /// <summary>The migration name (folder suffix).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The recorded source checksum.</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>The host stamp recorded on the row.</summary>
    public string AppliedBy { get; set; } = string.Empty;
}
