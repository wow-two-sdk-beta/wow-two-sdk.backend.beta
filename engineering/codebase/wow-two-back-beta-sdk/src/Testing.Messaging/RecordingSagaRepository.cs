using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// Delegating <see cref="ISagaRepository{TState}"/> that reconstructs a transition from the calls the coordinator makes:
/// a load pends one, the following write completes it, and a load with no write is the ignored path.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
internal sealed class RecordingSagaRepository<TState>(ISagaRepository<TState> inner, SagaRecorder<TState> recorder) : ISagaRepository<TState>
    where TState : class, ISagaState
{
    /// <summary>The repository under the recorder — the store itself, for a read that must not show up as a transition.</summary>
    public ISagaRepository<TState> Inner => inner;

    public async ValueTask<TState?> LoadAsync(string correlationId, CancellationToken cancellationToken)
    {
        var loaded = await inner.LoadAsync(correlationId, cancellationToken);

        if (SagaConsumeBracket.Current is { } bracket)
        {
            // A copy: the coordinator mutates what it loaded, and the from-state has to survive that.
            bracket.Pend(
                (typeof(TState), correlationId),
                new SagaPendingWrite<TState>
                {
                    Recorder = recorder,
                    CorrelationId = correlationId,
                    FromState = loaded?.CurrentState,
                    Loaded = loaded is null ? null : (TState)loaded.Copy(),
                    LoadedVersion = loaded?.Version ?? 0,
                    Envelope = bracket.Envelope,
                });
        }

        return loaded;
    }

    public ValueTask InsertAsync(TState state, CancellationToken cancellationToken)
        => WriteAsync(state, inner.InsertAsync, created: true, removed: false, cancellationToken);

    public ValueTask UpdateAsync(TState state, CancellationToken cancellationToken)
        => WriteAsync(state, inner.UpdateAsync, created: false, removed: false, cancellationToken);

    public ValueTask DeleteAsync(TState state, CancellationToken cancellationToken)
        => WriteAsync(state, inner.DeleteAsync, created: false, removed: true, cancellationToken);

    // Not a transition — a sweep of already-finalized instances, passed straight through to the real count.
    public ValueTask<int> PurgeFinalizedAsync(TimeSpan retention, CancellationToken cancellationToken)
        => inner.PurgeFinalizedAsync(retention, cancellationToken);

    private async ValueTask WriteAsync(
        TState state,
        Func<TState, CancellationToken, ValueTask> write,
        bool created,
        bool removed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var bracket = SagaConsumeBracket.Current;

        // The key carries TState, so nothing but this state type's pending load can come back.
        var pending = bracket?.Take((typeof(TState), state.CorrelationId)) as SagaPendingWrite<TState>;

        try
        {
            await write(state, cancellationToken);
        }
        catch (SagaConcurrencyException exception)
        {
            // Recorded, then rethrown untouched — the coordinator's reload-and-replay stays under test.
            Record(state, pending, bracket, SagaTransitionOutcome.Conflicted, created: false, removed: false, exception);
            throw;
        }

        Record(state, pending, bracket, SagaTransitionOutcome.Transitioned, created, removed, exception: null);
    }

    private void Record(
        TState state,
        SagaPendingWrite<TState>? pending,
        SagaConsumeBracket? bracket,
        SagaTransitionOutcome outcome,
        bool created,
        bool removed,
        Exception? exception)
        => recorder.Append(new RecordedTransition<TState>
        {
            CorrelationId = state.CorrelationId,

            // An insert has nothing loaded behind it — the coordinator starts the instance in Initial.
            FromState = pending?.FromState ?? (created ? SagaStateConstants.Initial : null),
            ToState = state.CurrentState,
            Outcome = outcome,
            Attempt = pending?.Attempt ?? 1,
            Envelope = bracket?.Envelope,
            Instance = (TState)state.Copy(),
            Version = state.Version,
            Created = created,
            Removed = removed,
            Exception = exception,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        });
}
