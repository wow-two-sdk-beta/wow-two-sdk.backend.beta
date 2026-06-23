using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Testing.Containers;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

/// <summary>A provider-switchable EF test database — a Postgres container (Respawn reset) or an in-memory SQLite database — behind one uniform API, selected by <see cref="TestSetupOptions"/>.</summary>
/// <remarks>Subclass per app to supply the model conventions via <see cref="CreateContext"/>; expose the subclass as an xUnit <c>ICollectionFixture</c> and reset per test with <see cref="ResetAsync"/>. Override <see cref="Provider"/> to pin one suite to a specific provider.</remarks>
/// <typeparam name="TContext">The application <see cref="DbContext"/> under test.</typeparam>
[SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Teardown runs in IAsyncLifetime.DisposeAsync, which xUnit invokes.")]
public abstract class RelationalTestDb<TContext> : IAsyncLifetime
    where TContext : DbContext
{
    private PostgresFixture? _postgres;
    private SqliteConnection? _sqlite;

    /// <summary>The provider this fixture uses; defaults to <see cref="TestSetupOptions.Current"/>. Override to pin a single suite to a specific provider.</summary>
    public virtual DatabaseProvider Provider => TestSetupOptions.Current.Database;

    /// <summary>The connection string of the active test database.</summary>
    public string ConnectionString => Provider == DatabaseProvider.Sqlite
        ? RequireSqlite().ConnectionString
        : RequirePostgres().ConnectionString;

    /// <summary>Builds the app's context with the test provider already applied; override to add model conventions (e.g. snake_case) and interceptors (e.g. audit), then construct it.</summary>
    /// <param name="builder">The options builder, already pointed at the test provider.</param>
    protected abstract TContext CreateContext(DbContextOptionsBuilder<TContext> builder);

    /// <summary>A new context bound to the active test database, with the app's conventions applied.</summary>
    public TContext NewContext()
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        if (Provider == DatabaseProvider.Sqlite)
            builder.UseSqlite(RequireSqlite());
        else
            builder.UseNpgsql(RequirePostgres().ConnectionString);

        return CreateContext(builder);
    }

    /// <summary>Starts the database and creates the schema (Postgres: pinned container + Respawn snapshot; SQLite: in-memory connection).</summary>
    public async Task InitializeAsync()
    {
        if (Provider == DatabaseProvider.Sqlite)
        {
            _sqlite = new SqliteConnection("DataSource=:memory:");
            await _sqlite.OpenAsync().ConfigureAwait(false);
            await CreateSchemaAsync().ConfigureAwait(false);
        }
        else
        {
            _postgres = new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build());
            await _postgres.StartAsync().ConfigureAwait(false);
            await CreateSchemaAsync().ConfigureAwait(false);
            await _postgres.InitializeRespawnerAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Resets the database to empty between tests (Postgres: Respawn truncate; SQLite: recreate the in-memory database).</summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Provider == DatabaseProvider.Sqlite)
        {
            // Dropping the held-open connection discards the in-memory database; a fresh one plus schema is the clean reset.
            if (_sqlite is not null)
                await _sqlite.DisposeAsync().ConfigureAwait(false);
            _sqlite = new SqliteConnection("DataSource=:memory:");
            await _sqlite.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CreateSchemaAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await RequirePostgres().ResetAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Disposes the SQLite connection or the Postgres container.</summary>
    public async Task DisposeAsync()
    {
        if (_sqlite is not null)
            await _sqlite.DisposeAsync().ConfigureAwait(false);
        if (_postgres is not null)
            await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    private SqliteConnection RequireSqlite() => _sqlite ?? throw new InvalidOperationException("SQLite test database not initialized — call StartAsync first.");

    private PostgresFixture RequirePostgres() => _postgres ?? throw new InvalidOperationException("Postgres test database not initialized — call StartAsync first.");
}
