using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// The harness's eyes on one saga's instances — every reaction to every message, as a
/// <see cref="RecordedTransition{TState}"/>.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
///   - recording changes no routing, settlement or concurrency behaviour
///   - under <see cref="SagaOptions.RemoveOnFinalize"/>, an instance created and finalized by one message records as <see cref="SagaTransitionOutcome.Ignored"/> (blind spot in <c>Testing.Messaging.md</c>)
///   - every member is thread-safe
/// </remarks>
public sealed class SagaRecorder<TState>
    where TState : class, ISagaState
{
    /// <summary>Every reaction recorded so far — awaitable, so a test waits for a state rather than sleeping toward it.</summary>
    /// <remarks>
    ///   - wait here, not on <see cref="MessagingTestHarness.Consumed"/>
    ///   - a write-free outcome (<see cref="SagaTransitionOutcome.Ignored"/>, <see cref="SagaTransitionOutcome.Faulted"/>) is recorded after the consume observers run
    /// </remarks>
    public RecordedTransitionLog<TState> Transitions { get; } = new();

    /// <summary>
    /// How many writes were rejected by the repository's version check. Each one costs a replay, never a lost update —
    /// a count that stays at zero in a test built to force a conflict means the repository is not enforcing its
    /// contract, which is the failure this whole path exists to catch.
    /// </summary>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for all.</param>
    public int ConcurrencyConflicts(string? correlationId = null)
        => Transitions.Count(transition => transition.Outcome == SagaTransitionOutcome.Conflicted && Matches(transition, correlationId));

    /// <summary>How many transitions landed only after being re-run against reloaded state.</summary>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for all.</param>
    public int Replays(string? correlationId = null)
        => Transitions.Count(transition => transition.Outcome == SagaTransitionOutcome.Transitioned && transition.Replayed && Matches(transition, correlationId));

    /// <summary>Empty the log — for a second phase of the same test.</summary>
    public void Reset() => Transitions.Clear();

    /// <summary>A one-line census of the edges taken — the payload of a harness timeout message.</summary>
    public override string ToString() => Transitions.ToString();

    internal void Append(RecordedTransition<TState> transition) => Transitions.Append(transition);

    private static bool Matches(RecordedTransition<TState> transition, string? correlationId)
        => correlationId is null || string.Equals(transition.CorrelationId, correlationId, StringComparison.Ordinal);
}
