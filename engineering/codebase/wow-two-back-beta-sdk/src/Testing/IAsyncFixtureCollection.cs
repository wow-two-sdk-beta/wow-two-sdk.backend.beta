namespace WoW.Two.Sdk.Backend.Beta.Testing;

/// <summary>
/// Aggregates multiple <see cref="IAsyncTestFixture"/>s and orchestrates start/stop/reset across them.
/// </summary>
public interface IAsyncFixtureCollection : IAsyncTestFixture
{
    /// <summary>The fixtures composed into this collection.</summary>
    IReadOnlyCollection<IAsyncTestFixture> Fixtures { get; }
}
