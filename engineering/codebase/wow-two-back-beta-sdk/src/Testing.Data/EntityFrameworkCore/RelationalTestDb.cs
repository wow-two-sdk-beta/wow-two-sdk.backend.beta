using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Testing.Containers.Postgres;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

/// <summary>An EF test database using one fixture-owned provider: Postgres with Respawn or in-memory SQLite.</summary>
/// <remarks>Subclass per app to supply the model conventions via <see cref="CreateContext"/>; expose the subclass as an xUnit <c>ICollectionFixture</c> and reset per test with <see cref="ResetAsync"/>. Override <see cref="Provider"/> to pin one suite to a specific provider.</remarks>
/// <typeparam name="TContext">The application <see cref="DbContext"/> under test.</typeparam>
[SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Teardown runs in IAsyncLifetime.DisposeAsync, which xUnit invokes.")]
public abstract class RelationalTestDb<TContext> : IAsyncLifetime
    where TContext : DbContext
{
    private readonly bool _ownsPostgres;
    private PostgresFixture? _postgres;
    private SqliteConnection? _sqlite;

    /// <summary>Creates a fixture that constructs and owns its own pinned Postgres container.</summary>
    protected RelationalTestDb() : this(DatabaseProvider.Postgres) { }

    /// <summary>Creates a fixture that owns the selected provider's resources.</summary>
    /// <param name="provider">The provider used by this fixture instance.</param>
    protected RelationalTestDb(DatabaseProvider provider)
    {
        Provider = provider;
        _ownsPostgres = true;
    }

    /// <summary>Creates a fixture over a pre-built <see cref="PostgresFixture"/> — one container shared by a whole test collection instead of one per class.</summary>
    /// <remarks>The caller keeps ownership: <see cref="DisposeAsync"/> leaves the supplied fixture alone. Pass an <em>unstarted</em> fixture; <see cref="InitializeAsync"/> starts it (<c>StartAsync</c> is idempotent, so an already-started one is fine too).</remarks>
    /// <param name="postgres">The externally-owned Postgres fixture this test database binds to.</param>
    protected RelationalTestDb(PostgresFixture postgres)
    {
        _postgres = postgres ?? throw new ArgumentNullException(nameof(postgres));
        Provider = DatabaseProvider.Postgres;
        _ownsPostgres = false;
    }

    /// <summary>The provider owned by this fixture instance.</summary>
    public DatabaseProvider Provider { get; }

    /// <summary>The connection string of the active test database.</summary>
    public string ConnectionString => Provider == DatabaseProvider.Sqlite
        ? RequireSqlite().ConnectionString
        : RequirePostgres().ConnectionString;

    /// <summary>Builds the app's context with the test provider and <see cref="ApplyConventions"/> already applied; override to add anything the constructor needs beyond the options.</summary>
    /// <param name="builder">The options builder, already pointed at the test provider.</param>
    protected abstract TContext CreateContext(DbContextOptionsBuilder<TContext> builder);

    /// <summary>Applies the app's provider-independent options — naming conventions, interceptors the app always carries — to <em>every</em> path that builds a context.</summary>
    /// <remarks>Override here rather than in <see cref="CreateContext"/>: the DI-built path (see <c>AddTestEntityFrameworkCore</c>) never calls <see cref="CreateContext"/>, so conventions applied only there produce a context that maps against a different schema than the one <see cref="InitializeAsync"/> created.</remarks>
    /// <param name="builder">The options builder to configure.</param>
    protected virtual void ApplyConventions(DbContextOptionsBuilder builder)
    {
        // Default: no conventions beyond the provider.
    }

    /// <summary>Points an arbitrary <see cref="DbContextOptionsBuilder"/> at the active test database and applies <see cref="ApplyConventions"/>.</summary>
    /// <remarks>The seam a DI-built context needs: pass this straight to <c>AddEntityFrameworkCore&lt;TContext&gt;</c> / <c>AddDbContext</c> so the registration's own pipeline (interceptor auto-wire, pooling branch) actually runs — <see cref="NewContext"/> builds options directly and runs neither.</remarks>
    /// <param name="builder">The options builder to point at the test database.</param>
    public void ApplyProvider(DbContextOptionsBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (Provider == DatabaseProvider.Sqlite)
            builder.UseSqlite(RequireSqlite());
        else
            builder.UseNpgsql(RequirePostgres().ConnectionString);

        ApplyConventions(builder);
    }

    /// <summary>A new context bound to the active test database, with the app's conventions applied.</summary>
    public TContext NewContext()
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        ApplyProvider(builder);
        return CreateContext(builder);
    }

    /// <summary>Opens a second, real <see cref="DbConnection"/> against the same database — the raw connection the Dapper tier and every cross-connection visibility assertion needs.</summary>
    /// <remarks>The caller owns and disposes the returned connection. Postgres only. On SQLite the connection string is <c>DataSource=:memory:</c>, so a second connection would open a different, empty database and every assertion made through it would pass vacuously — this throws instead of manufacturing that confidence.</remarks>
    /// <param name="cancellationToken">Token to cancel the open.</param>
    /// <exception cref="NotSupportedException">The active provider is SQLite.</exception>
    public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (Provider == DatabaseProvider.Sqlite)
            throw new NotSupportedException(
                "A second connection is not available on the in-memory SQLite provider: 'DataSource=:memory:' opens a different, empty database, so assertions through it pass on nothing. Pin the suite to Postgres (override Provider) for anything needing a real second connection.");

        var connection = new NpgsqlConnection(RequirePostgres().ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
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
            _postgres ??= new PostgresFixture(new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build());
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

    /// <summary>Disposes the SQLite connection or the Postgres container. A container supplied through the <see cref="RelationalTestDb{TContext}(PostgresFixture)"/> overload belongs to the caller and is left alone.</summary>
    public async Task DisposeAsync()
    {
        if (_sqlite is not null)
            await _sqlite.DisposeAsync().ConfigureAwait(false);
        if (_postgres is not null && _ownsPostgres)
            await _postgres.DisposeAsync().ConfigureAwait(false);
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var context = NewContext();
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    }

    private SqliteConnection RequireSqlite() => _sqlite ?? throw new InvalidOperationException("SQLite test database not initialized — call InitializeAsync first.");

    private PostgresFixture RequirePostgres() => _postgres is { IsStarted: true } started
        ? started
        : throw new InvalidOperationException("Postgres test database not initialized — call InitializeAsync first.");
}
