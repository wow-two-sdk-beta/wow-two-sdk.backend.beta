using System.Data.Common;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Dapper;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.Migrations;

/// <summary>One configured bespoke migrator over a real database — the DI graph the engine runs inside, plus direct-SQL assertion helpers, across Postgres and SQLite.</summary>
public sealed class MigratorHarness : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly DatabaseProvider _databaseProvider;
    private readonly IAsyncDisposable? _ownedDataSource;

    private MigratorHarness(ServiceProvider provider, DatabaseProvider databaseProvider, IAsyncDisposable? ownedDataSource)
    {
        _provider = provider;
        _databaseProvider = databaseProvider;
        _ownedDataSource = ownedDataSource;
    }

    /// <summary>The host-facing runner under test.</summary>
    public IMigrationRunnerService Runner => _provider.GetRequiredService<IMigrationRunnerService>();

    /// <summary>The effective options the engine runs under, after the configure hook.</summary>
    public MigrationOptions Options => _provider.GetRequiredService<MigrationOptions>();

    /// <summary>Builds a Postgres migrator over <paramref name="connectionString"/> reading filesystem migrations from <paramref name="migrationsRoot"/>.</summary>
    /// <param name="connectionString">The Postgres connection string, typically a container DB.</param>
    /// <param name="migrationsRoot">The on-disk <c>NNN-name</c> migrations root, typically a <see cref="MigrationsWorkspace.Root"/>.</param>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/>.</param>
    public static MigratorHarness CreatePostgres(string connectionString, string migrationsRoot, Action<MigrationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(migrationsRoot);

        return BuildPostgres(connectionString, services => services.AddDatabaseBespokeMigrations(migrationsRoot, Provider(DatabaseProvider.Postgres, configure)));
    }

    /// <summary>Builds a Postgres migrator over <paramref name="connectionString"/> reading embedded migrations from <paramref name="sqlAssembly"/>.</summary>
    /// <param name="connectionString">The Postgres connection string, typically a container DB.</param>
    /// <param name="sqlAssembly">The assembly embedding the real <c>Migrations/NNN-name/*.sql</c> resources.</param>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/>.</param>
    public static MigratorHarness CreatePostgres(string connectionString, Assembly sqlAssembly, Action<MigrationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentNullException.ThrowIfNull(sqlAssembly);

        return BuildPostgres(connectionString, services => services.AddDatabaseBespokeMigrations(sqlAssembly, Provider(DatabaseProvider.Postgres, configure)));
    }

    /// <summary>Builds a SQLite migrator over <paramref name="connectionString"/> reading filesystem migrations from <paramref name="migrationsRoot"/>.</summary>
    /// <param name="connectionString">The SQLite connection string, e.g. a temp file.</param>
    /// <param name="migrationsRoot">The on-disk <c>NNN-name</c> migrations root, typically a <see cref="MigrationsWorkspace.Root"/>.</param>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/>.</param>
    public static MigratorHarness CreateSqlite(string connectionString, string migrationsRoot, Action<MigrationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(migrationsRoot);

        return BuildSqlite(connectionString, services => services.AddDatabaseBespokeMigrations(migrationsRoot, Provider(DatabaseProvider.Sqlite, configure)));
    }

    /// <summary>Builds a SQLite migrator over <paramref name="connectionString"/> reading embedded migrations from <paramref name="sqlAssembly"/>.</summary>
    /// <param name="connectionString">The SQLite connection string, e.g. a temp file.</param>
    /// <param name="sqlAssembly">The assembly embedding the real <c>Migrations/NNN-name/*.sql</c> resources.</param>
    /// <param name="configure">An optional hook to override <see cref="MigrationOptions"/>.</param>
    public static MigratorHarness CreateSqlite(string connectionString, Assembly sqlAssembly, Action<MigrationOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentNullException.ThrowIfNull(sqlAssembly);

        return BuildSqlite(connectionString, services => services.AddDatabaseBespokeMigrations(sqlAssembly, Provider(DatabaseProvider.Sqlite, configure)));
    }

    /// <summary>Opens a connection via the SDK factory the runner uses, for direct database assertions.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) =>
        _provider.GetRequiredService<IDbConnectionFactory>().CreateOpenAsync(cancellationToken);

    /// <summary>Reads every <c>migration_history</c> row ordered by ordinal — the source of truth for recording assertions.</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<IReadOnlyList<MigrationHistoryRow>> ReadHistoryAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<MigrationHistoryRow>(
            "select ordinal, name, checksum, applied_by as AppliedBy from migration_history order by ordinal").ConfigureAwait(false);
        return rows.AsList();
    }

    /// <summary>Returns whether a table exists in the database, across the Postgres and SQLite catalogs.</summary>
    /// <param name="table">The table name to probe.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<bool> HasTableAsync(string table, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return _databaseProvider == DatabaseProvider.Sqlite
            ? await connection.ExecuteScalarAsync<long>(
                "select count(*) from sqlite_master where type = 'table' and name = @table", new { table }).ConfigureAwait(false) > 0
            : await connection.ExecuteScalarAsync<bool>(
                "select exists (select 1 from information_schema.tables where table_schema = 'public' and table_name = @table)", new { table }).ConfigureAwait(false);
    }

    /// <summary>Returns whether an index exists in the database, across the Postgres and SQLite catalogs.</summary>
    /// <param name="index">The index name to probe.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<bool> HasIndexAsync(string index, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(index);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return _databaseProvider == DatabaseProvider.Sqlite
            ? await connection.ExecuteScalarAsync<long>(
                "select count(*) from sqlite_master where type = 'index' and name = @index", new { index }).ConfigureAwait(false) > 0
            : await connection.ExecuteScalarAsync<bool>(
                "select exists (select 1 from pg_indexes where schemaname = 'public' and indexname = @index)", new { index }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync().ConfigureAwait(false);
        if (_ownedDataSource is not null)
            await _ownedDataSource.DisposeAsync().ConfigureAwait(false);
    }

    private static MigratorHarness BuildPostgres(string connectionString, Action<IServiceCollection> addMigrations)
    {
        var services = NewServices();

        // Postgres exposes a DbDataSource — the seam the SDK's DataSourceConnectionFactory binds to (the production path).
        var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
        services.AddSingleton<DbDataSource>(dataSource);
        services.AddDataSourceConnectionFactory();
        addMigrations(services);

        // The provider does not own an instance registered via AddSingleton(instance), so the harness disposes it.
        return new MigratorHarness(services.BuildServiceProvider(), DatabaseProvider.Postgres, dataSource);
    }

    private static MigratorHarness BuildSqlite(string connectionString, Action<IServiceCollection> addMigrations)
    {
        var services = NewServices();

        // SQLite has no DbDataSource, so the dedicated SQLite factory is the seam (the production adoption path).
        services.AddSqliteConnectionFactory(connectionString);
        addMigrations(services);

        return new MigratorHarness(services.BuildServiceProvider(), DatabaseProvider.Sqlite, ownedDataSource: null);
    }

    private static ServiceCollection NewServices()
    {
        // Dapper maps snake_case history columns (applied_at to AppliedAt) — the convention the product sets.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSimpleConsole());
        return services;
    }

    private static Action<MigrationOptions> Provider(DatabaseProvider provider, Action<MigrationOptions>? configure) =>
        options =>
        {
            options.Provider = provider;
            configure?.Invoke(options);
        };
}
