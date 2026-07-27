using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Data.EntityFrameworkCore;

/// <summary>Convenience base for EF tests sharing one <see cref="RelationalTestDb{TContext}"/> across a collection — resets the database before each test.</summary>
/// <remarks>The counterpart to <c>MigratorTestBase</c> for the EF tier. The house pattern of one container per test class costs a container per class; a collection fixture plus this per-test reset costs one container per suite.</remarks>
/// <typeparam name="TDb">The concrete test-database fixture, supplied by the test collection.</typeparam>
/// <typeparam name="TContext">The application context under test.</typeparam>
/// <param name="testDb">The shared, already-started test database.</param>
public abstract class RelationalTestBase<TDb, TContext>(TDb testDb) : IAsyncLifetime
    where TDb : RelationalTestDb<TContext>
    where TContext : DbContext
{
    /// <summary>The shared test database.</summary>
    protected TDb TestDb { get; } = testDb;

    /// <summary>Truncates the database so the test starts clean.</summary>
    public virtual async Task InitializeAsync() => await TestDb.ResetAsync().ConfigureAwait(false);

    /// <summary>No teardown by default — the database is owned by the collection fixture, not the test.</summary>
    public virtual Task DisposeAsync() => Task.CompletedTask;
}
