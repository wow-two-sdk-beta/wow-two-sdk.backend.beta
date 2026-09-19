using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// Accesses saga state while simulating one optimistic-concurrency conflict: another writer commits between this caller's
/// load and its write, so the version the caller holds is stale and the store rejects it.
/// </summary>
/// <remarks>
/// Everything else delegates to the shipped <see cref="InMemorySagaRepository{TState}"/>, so the conflict is produced by
/// the real optimistic-concurrency check rather than by a hand-thrown exception.
/// </remarks>
public sealed class ConflictOnceSagaRepository(TimeProvider timeProvider) : ISagaRepository<OrderSagaState>
{
    private readonly InMemorySagaRepository<OrderSagaState> _inner = new(timeProvider);
    private int _updates;

    /// <summary>Instances currently stored.</summary>
    public int Count => _inner.Count;

    /// <inheritdoc />
    public ValueTask<OrderSagaState?> LoadAsync(string correlationId, CancellationToken cancellationToken)
        => _inner.LoadAsync(correlationId, cancellationToken);

    /// <inheritdoc />
    public ValueTask InsertAsync(OrderSagaState state, CancellationToken cancellationToken)
        => _inner.InsertAsync(state, cancellationToken);

    /// <inheritdoc />
    public async ValueTask UpdateAsync(OrderSagaState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (Interlocked.Increment(ref _updates) == 1)
        {
            var concurrent = await _inner.LoadAsync(state.CorrelationId, cancellationToken);
            if (concurrent is not null)
            {
                concurrent.Interference++;
                await _inner.UpdateAsync(concurrent, cancellationToken); // commits first; the caller's version is now stale
            }
        }

        await _inner.UpdateAsync(state, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(OrderSagaState state, CancellationToken cancellationToken)
        => _inner.DeleteAsync(state, cancellationToken);
}
