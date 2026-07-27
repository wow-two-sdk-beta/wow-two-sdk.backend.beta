namespace WoW.Two.Sdk.Backend.Beta.Testing;

/// <summary>
/// Default <see cref="IAsyncFixtureCollection"/> — starts all fixtures sequentially,
/// resets sequentially, disposes in reverse order.
/// </summary>
public sealed class AsyncFixtureCollection : IAsyncFixtureCollection
{
    private readonly List<IAsyncTestFixture> _fixtures;

    /// <summary>Creates an empty collection.</summary>
    public AsyncFixtureCollection() => _fixtures = [];

    /// <summary>Creates a collection seeded with the given fixtures.</summary>
    public AsyncFixtureCollection(IEnumerable<IAsyncTestFixture> fixtures)
        => _fixtures = [.. fixtures];

    /// <inheritdoc />
    public string Name => "fixture-collection";

    /// <inheritdoc />
    public IReadOnlyCollection<IAsyncTestFixture> Fixtures => _fixtures;

    /// <summary>Add a fixture to the collection. Returns the collection for chaining.</summary>
    public AsyncFixtureCollection Add(IAsyncTestFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixtures.Add(fixture);
        return this;
    }

    /// <inheritdoc />
    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        foreach (var fixture in _fixtures)
            await fixture.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask ResetAsync(CancellationToken cancellationToken = default)
    {
        foreach (var fixture in _fixtures)
            await fixture.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        for (var i = _fixtures.Count - 1; i >= 0; i--)
            await _fixtures[i].DisposeAsync().ConfigureAwait(false);

        _fixtures.Clear();
    }
}
