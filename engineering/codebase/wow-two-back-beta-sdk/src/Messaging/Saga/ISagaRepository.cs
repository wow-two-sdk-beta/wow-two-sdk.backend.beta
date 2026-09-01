using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// Stores saga instances by correlation id. The default is in-memory; a durable implementation (EF, Mongo, Redis) maps
/// the same four operations onto its own store and is registered in its place.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
///   - Guard the unique correlation id on insert, throwing <see cref="SagaConcurrencyException"/> on a duplicate
///   - Guard the stored <see cref="ISagaState.Version"/> on update and delete
///   - a successful write increments <see cref="ISagaState.Version"/> on the stored copy and on the passed instance
/// </remarks>
public interface ISagaRepository<TState>
    where TState : class, ISagaState
{
    /// <summary>Load an instance, or null when none exists for the key.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken);

    /// <summary>Store a new instance. Throws <see cref="SagaConcurrencyException"/> when the correlation id already exists.</summary>
    /// <param name="state">The new instance; its version moves 0 → 1 on success.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask InsertAsync(TState state, CancellationToken cancellationToken);

    /// <summary>Overwrite an instance. Throws <see cref="SagaConcurrencyException"/> when the stored version moved on.</summary>
    /// <param name="state">The instance as loaded and mutated; its version increments on success.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask UpdateAsync(TState state, CancellationToken cancellationToken);

    /// <summary>Remove a finalized instance. Throws <see cref="SagaConcurrencyException"/> when the stored version moved on.</summary>
    /// <param name="state">The instance to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeleteAsync(TState state, CancellationToken cancellationToken);

    /// <summary>
    /// Remove instances finalized longer ago than <paramref name="retention"/>; returns how many were removed. Only
    /// meaningful when <see cref="SagaOptions.RemoveOnFinalize"/> is off — that is the mode that keeps a finished
    /// instance around for inspection and needs a sweeper. No-op by default.
    /// </summary>
    /// <param name="retention">How long a finalized instance is kept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> PurgeFinalizedAsync(TimeSpan retention, CancellationToken cancellationToken) => ValueTask.FromResult(0);
}
