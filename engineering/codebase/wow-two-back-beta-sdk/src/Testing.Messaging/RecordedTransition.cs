using System.Collections.Concurrent;
using System.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// One saga instance's reaction to one message, as the harness saw it at the repository: which state it was in, what
/// arrived, and which state was written.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
///   - reconstructed from the coordinator's repository calls
///   - paired with the message being consumed at the time
///   - <see cref="MessagingTestHarness"/>'s message logs never show a transition
/// </remarks>
public sealed record RecordedTransition<TState>
    where TState : class, ISagaState
{
    /// <summary>The instance's correlation id.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>The state the instance was loaded in; <c>null</c> when no instance existed — an <c>Initially</c> clause created one, or nothing did.</summary>
    public required string? FromState { get; init; }

    /// <summary>The state written. Equal to <see cref="FromState"/> when the outcome moved nothing.</summary>
    public required string? ToState { get; init; }

    /// <summary>How the reaction ended.</summary>
    public required SagaTransitionOutcome Outcome { get; init; }

    /// <summary>
    /// Which pass over this instance produced the record, from 1. Higher than 1 means the coordinator reloaded and
    /// re-ran the transition after losing an optimistic-concurrency race.
    /// </summary>
    public required int Attempt { get; init; }

    /// <summary>The message being consumed when the saga reacted; <c>null</c> when the repository was called outside the consume pipeline.</summary>
    public EventEnvelope? Envelope { get; init; }

    /// <summary>The event payload that drove the reaction.</summary>
    public object? Event => Envelope?.Body;

    /// <summary>Runtime type of <see cref="Event"/>.</summary>
    public Type? EventType => Envelope?.BodyType;

    /// <summary>The instance as it stood at this point — written on a <see cref="SagaTransitionOutcome.Transitioned"/> record, as loaded otherwise. An independent copy, so a later transition does not mutate it.</summary>
    public TState? Instance { get; init; }

    /// <summary>The instance's optimistic-concurrency version after the write; the loaded version when nothing was written.</summary>
    public int Version { get; init; }

    /// <summary>The write was the insert that created the instance.</summary>
    public bool Created { get; init; }

    /// <summary>The write was the delete that removes a finalized instance (<see cref="SagaOptions.RemoveOnFinalize"/>).</summary>
    public bool Removed { get; init; }

    /// <summary>The failure, on a <see cref="SagaTransitionOutcome.Conflicted"/> or <see cref="SagaTransitionOutcome.Faulted"/> record.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Wall-clock time the harness recorded this — for ordering against the message logs in a failure dump.</summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    /// <summary>The instance reached <see cref="SagaStateConstants.Final"/> on this record.</summary>
    public bool Finalized => Outcome == SagaTransitionOutcome.Transitioned && string.Equals(ToState, SagaStateConstants.Final, StringComparison.Ordinal);

    /// <summary>This transition ran again after a version conflict — the behaviour that is silently wrong when the repository does not enforce its version check.</summary>
    public bool Replayed => Attempt > 1;

    /// <summary>True when the driving event is a <typeparamref name="TEvent"/> (assignability, so a base type or interface matches too).</summary>
    /// <typeparam name="TEvent">The event contract to test for.</typeparam>
    public bool Is<TEvent>()
        where TEvent : class, IEvent
        => Envelope?.Body is TEvent;

    /// <summary>The driving event as a <typeparamref name="TEvent"/>.</summary>
    /// <typeparam name="TEvent">The event contract to cast to.</typeparam>
    /// <exception cref="InvalidCastException">The driving event is not a <typeparamref name="TEvent"/>.</exception>
    public TEvent EventAs<TEvent>()
        where TEvent : class, IEvent
        => Envelope?.Body as TEvent ?? throw new InvalidCastException($"Transition on '{CorrelationId}' was driven by {EventType?.Name ?? "nothing"}, not by a {typeof(TEvent).Name}.");

    /// <summary>The record as <c>from --Event--&gt; to</c>, which is how it reads in a timeout census.</summary>
    public override string ToString()
    {
        var edge = $"{FromState ?? "(none)"} --{EventType?.Name ?? "?"}--> {ToState ?? "(none)"}";
        return Outcome == SagaTransitionOutcome.Transitioned ? edge : $"{edge} [{Outcome}]";
    }
}
