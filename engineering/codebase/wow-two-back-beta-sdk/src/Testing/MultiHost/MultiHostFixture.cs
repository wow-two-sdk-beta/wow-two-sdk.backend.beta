using Microsoft.AspNetCore.Mvc.Testing;

namespace WoW.Two.Sdk.Backend.Beta.Testing.MultiHost;

/// <summary>
/// Base for an integration-test fixture that boots <b>several in-process web hosts over one set of
/// shared backing services</b> — e.g. a management API and a redirect API sharing a single Postgres
/// container, or any "two services, one database" topology.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it orchestrates</b> (one shared lifecycle for the whole test collection):
/// </para>
/// <list type="number">
///   <item><b>Start</b> the shared backing fixtures (containers, etc.) via the composed <see cref="IAsyncFixtureCollection"/>.</item>
///   <item><b>Inject</b> environment / configuration so every host targets those shared services (<see cref="ConfigureEnvironment"/>).</item>
///   <item><b>Build</b> every registered host, forcing each to materialize — the first build runs the startup migrations against the shared store; later builds see the migrated schema.</item>
///   <item><b>Initialize shared state</b> after migrations (<see cref="InitializeStateAsync"/>) — e.g. snapshot the post-migration schema for a between-test reset.</item>
///   <item><b>Reset</b> per test by delegating to the shared collection's <see cref="IAsyncTestFixture.ResetAsync"/> — never to any one container's internals.</item>
///   <item><b>Dispose</b> the hosts, then the shared fixtures (reverse order).</item>
/// </list>
/// <para>
/// <b>Decoupling.</b> The base talks to its backing services only through the <see cref="IAsyncTestFixture"/>
/// contract (<see cref="IAsyncTestFixture.StartAsync"/> / <see cref="IAsyncTestFixture.ResetAsync"/> /
/// <see cref="IAsyncDisposable.DisposeAsync"/>). It does <i>not</i> know about Postgres, Respawn, or any
/// concrete fixture. Anything provider-specific that must run <i>after</i> migrations (such as building a
/// Respawn snapshot) goes in the app's <see cref="InitializeStateAsync"/> override, where the concrete
/// fixture type is in scope.
/// </para>
/// <para>
/// <b><c>extern alias</c> requirement.</b> When two entry points both expose the top-level
/// <c>Program</c> symbol (the default for minimal-hosting apps, each emitting <c>Program</c> in its
/// assembly's global namespace), the two <c>Program</c> types are ambiguous in one test project.
/// Alias each referenced app project in the test <c>.csproj</c> and bind the aliases at the top of the
/// fixture file:
/// <code>
/// &lt;!-- test .csproj --&gt;
/// &lt;ProjectReference Include="..\App.Api\App.Api.csproj"      Aliases="apihost" /&gt;
/// &lt;ProjectReference Include="..\App.Redirect\App.Redirect.csproj" Aliases="redirecthost" /&gt;
/// </code>
/// <code>
/// // top of the fixture file
/// extern alias apihost;
/// extern alias redirecthost;
/// using ApiProgram      = apihost::Program;
/// using RedirectProgram = redirecthost::Program;
/// </code>
/// A single host needs no alias.
/// </para>
/// </remarks>
public abstract class MultiHostFixture : IAsyncDisposable
{
    private readonly AsyncFixtureCollection _shared = new();
    private readonly List<IDisposable> _hosts = [];
    private readonly List<Action> _builders = [];
    private bool _started;
    private bool _disposed;

    /// <summary>The shared backing fixtures (containers, etc.), started before any host builds.</summary>
    protected IAsyncFixtureCollection Shared => _shared;

    /// <summary>Whether <see cref="StartAsync"/> has completed.</summary>
    public bool IsStarted => _started;

    /// <summary>
    /// Registers a shared backing fixture (e.g. a Postgres container). Call from the derived constructor.
    /// Order matters: fixtures start in registration order and dispose in reverse.
    /// </summary>
    /// <typeparam name="TFixture">The fixture type.</typeparam>
    /// <param name="fixture">The fixture instance.</param>
    /// <returns>The same instance, so the derived class can keep a typed reference.</returns>
    protected TFixture AddSharedFixture<TFixture>(TFixture fixture)
        where TFixture : IAsyncTestFixture
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
            throw new InvalidOperationException("Cannot add a shared fixture after StartAsync has run.");

        _shared.Add(fixture);
        return fixture;
    }

    /// <summary>
    /// Registers a host. Call from the derived constructor (typically wrapped in a typed property).
    /// The host is built — and so runs any startup migration — during <see cref="StartAsync"/>,
    /// in registration order.
    /// </summary>
    /// <typeparam name="TEntryPoint">The host's entry-point type (its aliased <c>Program</c>).</typeparam>
    /// <param name="host">The host instance.</param>
    /// <returns>The same instance, so the derived class can expose it and create clients from it.</returns>
    protected WebApiTestHost<TEntryPoint> AddHost<TEntryPoint>(WebApiTestHost<TEntryPoint> host)
        where TEntryPoint : class
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
            throw new InvalidOperationException("Cannot add a host after StartAsync has run.");

        _hosts.Add(host);
        // Capture the typed build delegate here, where TEntryPoint is in scope — _hosts is a
        // non-generic IDisposable list (for reverse-order disposal) and can't infer TEntryPoint.
        _builders.Add(() => BuildHost(host));
        return host;
    }

    /// <summary>
    /// Boots the whole topology: start shared fixtures → inject env/config → build every host
    /// (the first build migrates the shared store) → initialize shared post-migration state.
    /// Idempotent — a second call is a no-op.
    /// </summary>
    /// <param name="cancellationToken">Cancels container startup.</param>
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;

        // 1. Shared backing services up first — connection strings / endpoints exist after this.
        await _shared.StartAsync(cancellationToken).ConfigureAwait(false);

        // 2. Point the (not-yet-built) hosts at those shared services. The env overlay typically wins
        //    over in-memory config, so env vars are the authoritative cross-host seam.
        ConfigureEnvironment();

        // 3. Build each host. Touching `.Services` forces the host to build; the first one to build
        //    runs the startup migrations against the shared store, the rest observe the migrated schema.
        foreach (var build in _builders)
            build();

        // 4. App-supplied, provider-specific init that must observe the MIGRATED schema
        //    (e.g. snapshot the DB for a Respawn-backed reset).
        await InitializeStateAsync(cancellationToken).ConfigureAwait(false);

        _started = true;
    }

    /// <summary>
    /// Resets state between tests by delegating to the shared collection's
    /// <see cref="IAsyncTestFixture.ResetAsync"/>. Host-agnostic and container-agnostic —
    /// it relies only on the <see cref="IAsyncTestFixture"/> contract.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reset.</param>
    public ValueTask ResetAsync(CancellationToken cancellationToken = default)
        => _shared.ResetAsync(cancellationToken);

    /// <summary>
    /// Override to inject environment variables / configuration so every host targets the shared
    /// fixtures (e.g. <c>Environment.SetEnvironmentVariable("APP_DB_CONNECTION", pg.ConnectionString)</c>).
    /// Runs after the shared fixtures start but before any host builds, so connection strings are available.
    /// </summary>
    protected virtual void ConfigureEnvironment() { }

    /// <summary>
    /// Override for provider-specific initialization that must run <b>after</b> the hosts have applied
    /// migrations — most commonly snapshotting the post-migration schema for a between-test reset
    /// (e.g. <c>await pg.InitializeRespawnerAsync()</c>). The concrete fixture type is in scope here,
    /// keeping the base decoupled from it. Default: no-op.
    /// </summary>
    /// <param name="cancellationToken">Cancels the initialization.</param>
    protected virtual ValueTask InitializeStateAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Forces a registered host to build (and thus run its startup migration). Touching
    /// <see cref="WebApplicationFactory{TEntryPoint}.Services"/> is what materializes the host.
    /// </summary>
    private static void BuildHost<TEntryPoint>(WebApiTestHost<TEntryPoint> host)
        where TEntryPoint : class
        => _ = host.Services;

    /// <summary>Disposes the hosts (reverse registration order), then the shared fixtures.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        for (var i = _hosts.Count - 1; i >= 0; i--)
            _hosts[i].Dispose();
        _hosts.Clear();

        await _shared.DisposeAsync().ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }
}
