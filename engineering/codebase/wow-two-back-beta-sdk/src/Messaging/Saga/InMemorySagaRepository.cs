using System.Collections.Concurrent;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>
/// In-memory <see cref="ISagaRepository{TState}"/> — the zero-infrastructure default. Instances live for the process,
/// so a restart loses every running saga; swap in a durable repository before a saga outlives a deployment.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
/// <para>
/// It implements the same optimistic-concurrency contract a durable repository must, rather than a simplified one, so a
/// concurrency bug shows up in a unit test instead of first appearing in production against a real database:
/// </para>
/// <list type="bullet">
///   <item><description>every stored value is a <see cref="ISagaState.Copy"/>, so a caller cannot mutate the store through the object it loaded;</description></item>
///   <item><description>an insert is a <see cref="ConcurrentDictionary{TKey,TValue}.TryAdd"/> — the dictionary key plays the role of the unique index that makes a double-initiate race safe;</description></item>
///   <item><description>an update or delete compares the stored version and swaps atomically, so the loser of a race is rejected rather than silently overwriting the winner.</description></item>
/// </list>
/// </remarks>
public sealed class InMemorySagaRepository<TState> : ISagaRepository<TState>
    where TState : class, ISagaState
{
    private readonly ConcurrentDictionary<string, TState> _instances = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>Create the repository.</summary>
    /// <param name="timeProvider">Clock used to age finalized instances during a purge.</param>
    public InMemorySagaRepository(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <summary>Instances currently stored — running ones, plus finalized ones still inside their retention window.</summary>
    public int Count => _instances.Count;

    /// <inheritdoc />
    public ValueTask<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        // A copy, not the stored instance: a handler mutating what it loaded must not reach into the store, or the
        // version check would compare an object against itself and every conflict would pass.
        return _instances.TryGetValue(correlationId, out var stored)
            ? ValueTask.FromResult<TState?>((TState)stored.Copy())
            : ValueTask.FromResult<TState?>(null);
    }

    /// <inheritdoc />
    public ValueTask InsertAsync(TState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.CorrelationId);
        cancellationToken.ThrowIfCancellationRequested();

        var stored = (TState)state.Copy();
        stored.Version = state.Version + 1;

        if (!_instances.TryAdd(state.CorrelationId, stored))
            throw SagaConcurrencyException.For(state.CorrelationId, state.Version);

        state.Version = stored.Version;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask UpdateAsync(TState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.CorrelationId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_instances.TryGetValue(state.CorrelationId, out var current) || current.Version != state.Version)
            throw SagaConcurrencyException.For(state.CorrelationId, state.Version);

        var updated = (TState)state.Copy();
        updated.Version = state.Version + 1;

        // Compare-and-swap against the exact instance that was read: between the check above and here, another writer
        // may have committed, and TryUpdate is what rejects this write instead of clobbering theirs.
        if (!_instances.TryUpdate(state.CorrelationId, updated, current))
            throw SagaConcurrencyException.For(state.CorrelationId, state.Version);

        state.Version = updated.Version;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DeleteAsync(TState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(state.CorrelationId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_instances.TryGetValue(state.CorrelationId, out var current) || current.Version != state.Version)
            throw SagaConcurrencyException.For(state.CorrelationId, state.Version);

        if (!_instances.TryRemove(new KeyValuePair<string, TState>(state.CorrelationId, current)))
            throw SagaConcurrencyException.For(state.CorrelationId, state.Version);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<int> PurgeFinalizedAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider.GetUtcNow() - retention;
        var removed = 0;

        foreach (var (correlationId, stored) in _instances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stored.FinalizedAtUtc is { } finalizedAt && finalizedAt <= cutoff && _instances.TryRemove(new KeyValuePair<string, TState>(correlationId, stored)))
                removed++;
        }

        return ValueTask.FromResult(removed);
    }
}
