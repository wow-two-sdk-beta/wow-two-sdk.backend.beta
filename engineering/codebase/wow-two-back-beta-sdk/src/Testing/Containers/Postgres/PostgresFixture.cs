using System.Data.Common;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Backend.Beta.Testing.Containers;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres;

/// <summary>
/// Async fixture spinning up a PostgreSQL container with a Respawn-backed <see cref="ResetAsync"/>
/// and an open <see cref="Connection"/> for between-test data wipes.
/// </summary>
/// <remarks>
/// Lifecycle for consumers: <c>StartAsync</c> the container → let the host apply migrations →
/// call <see cref="InitializeRespawnerAsync"/> once (snapshots the post-migration schema) →
/// <see cref="ResetAsync"/> before/after each test to truncate data.
/// The migration-history table is ignored so reseting never re-runs or desyncs migrations.
/// </remarks>
public sealed class PostgresFixture : ContainerFixtureBase<PostgreSqlContainer>
{
    // The migrator's bookkeeping table — never truncate, or migrations re-run/desync.
    private const string MigrationHistoryTable = "migration_history";

    private DbConnection? _connection;
    private Respawner? _respawner;

    /// <summary>Default constructor — uses the default Testcontainers Postgres image.</summary>
    public PostgresFixture() : this(new PostgreSqlBuilder().Build()) { }

    /// <summary>Constructor accepting a pre-configured <see cref="PostgreSqlContainer"/>.</summary>
    public PostgresFixture(PostgreSqlContainer container) : base(container) { }

    /// <inheritdoc />
    public override string Name => "postgres";

    /// <summary>The connection string of the started container.</summary>
    public string ConnectionString => Container.GetConnectionString();

    /// <summary>An open <see cref="DbConnection"/> against the container DB (used by Respawn). Valid after <c>StartAsync</c>.</summary>
    public DbConnection Connection =>
        _connection ?? throw new InvalidOperationException("PostgresFixture not started — Connection is null.");

    /// <summary>
    /// Builds the Respawner from the open connection — <b>must run after the host has applied migrations</b>,
    /// so the snapshot reflects the real schema. Safe to call again to re-snapshot after a schema change.
    /// </summary>
    public async ValueTask InitializeRespawnerAsync(CancellationToken cancellationToken = default)
    {
        _connection ??= await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            SchemasToInclude = ["public"],
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Table(MigrationHistoryTable)],
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <summary>Truncates every data table (everything except <c>migration_history</c>) via Respawn.</summary>
    public override async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        if (_respawner is null || _connection is null)
            return; // nothing to reset until the respawner is initialized

        await _respawner.ResetAsync(_connection).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync().ConfigureAwait(false);

        await base.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
