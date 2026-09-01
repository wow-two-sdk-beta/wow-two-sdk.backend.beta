using Microsoft.AspNetCore.Mvc.Testing;

namespace WoW.Two.Sdk.Backend.Beta.Testing.MultiHost;

/// <summary>
/// Base for an integration-test fixture that boots <b>several in-process web hosts over one set of
/// shared backing services</b> — e.g. a management API and a redirect API sharing a single Postgres
/// container, or any "two services, one database" topology.
/// </summary>
/// <remarks>
///   - register the shared fixtures and hosts in the derived constructor
///   - put provider-specific post-migration init in <see cref="InitializeStateAsync"/>
///   - alias each <c>ProjectReference</c> when two hosts both name their entry point <c>Program</c> (setup in <c>MultiHost.md</c>)
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
        // Capture the typed build delegate while TEntryPoint is in scope.
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

        // 2. Point the (not-yet-built) hosts at those shared services, via the env overlay.
        ConfigureEnvironment();

        // 3. Build each host — the first build runs the startup migrations against the shared store.
        foreach (var build in _builders)
            build();

        // 4. App-supplied init that must observe the migrated schema.
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
