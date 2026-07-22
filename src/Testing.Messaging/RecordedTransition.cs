using System.Collections.Concurrent;
using System.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>How one saga instance's reaction to one message ended.</summary>
public enum SagaTransitionOutcome
{
    /// <summary>The transition ran and its state was written — an insert, an update, or the delete that removes a finalized instance.</summary>
    Transitioned = 0,

    /// <summary>
    /// The write lost an optimistic-concurrency race. The coordinator answers by reloading and re-running, so a
    /// <see cref="Transitioned"/> record for the same instance follows at a higher
    /// <see cref="RecordedTransition{TState}.Attempt"/> — unless the retry budget ran out, in which case nothing does.
    /// </summary>
    Conflicted = 1,

    /// <summary>
    /// The instance was read and nothing was written: no instance existed and no clause initiates one, the current state
    /// has no clause for this event, or the message was a timeout the instance no longer expects.
    /// <see cref="RecordedTransition{TState}.FromState"/> tells the three apart — it is <c>null</c> only for the first.
    /// </summary>
    Ignored = 2,

    /// <summary>The message threw while the saga was handling it — <see cref="SagaMissingInstance.Fault"/>, an exhausted concurrency budget, or an activity that failed. One record per message, not per delivery attempt.</summary>
    Faulted = 3,
}

/// <summary>
/// One saga instance's reaction to one message, as the harness saw it at the repository: which state it was in, what
/// arrived, and which state was written.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
/// Reconstructed from the repository calls the coordinator makes — a load, then a write or the absence of one — paired
/// with the message being consumed at the time. That is the whole of what a saga does that is externally observable:
/// unlike a published event, a transition never crosses the bus, so the observer seam the
/// <see cref="MessagingTestHarness"/> is built on cannot see it.
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

    /// <summary>The instance reached <see cref="SagaStates.Final"/> on this record.</summary>
    public bool Finalized => Outcome == SagaTransitionOutcome.Transitioned && string.Equals(ToState, SagaStates.Final, StringComparison.Ordinal);

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

/// <summary>
/// Every saga reaction the harness recorded, in order — append-only, thread-safe, and awaitable: a test asserts on what
/// is already there, or awaits a state the instance has not reached yet.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
/// The waits are signal-driven, not polled: each append completes the pending waiters, so a test blocks exactly as long
/// as the saga takes. A wait that runs out of budget throws a <see cref="TimeoutException"/> naming what it wanted and
/// dumping the edges actually taken — the census is the whole point, because a saga that stalled and a saga that took a
/// different edge fail identically without it.
/// </remarks>
public sealed class RecordedTransitionLog<TState>
    where TState : class, ISagaState
{
    /// <summary>Overall budget for a wait that does not pass its own timeout.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<RecordedTransition<TState>> _transitions = new();
    private readonly Lock _sync = new();
    private TaskCompletionSource _appended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal RecordedTransitionLog()
    {
    }

    /// <summary>Everything recorded so far, in order. A snapshot — later appends do not mutate it.</summary>
    public IReadOnlyList<RecordedTransition<TState>> All => [.. _transitions];

    /// <summary>Every record for one instance, in order.</summary>
    /// <param name="correlationId">The instance's correlation id.</param>
    public IReadOnlyList<RecordedTransition<TState>> For(string correlationId)
        => [.. _transitions.Where(transition => string.Equals(transition.CorrelationId, correlationId, StringComparison.Ordinal))];

    /// <summary>How many records match <paramref name="match"/>; every record when it is <c>null</c>.</summary>
    /// <param name="match">Optional predicate.</param>
    public int Count(Func<RecordedTransition<TState>, bool>? match = null)
        => match is null ? _transitions.Count : _transitions.Count(match);

    /// <summary>Whether anything matching <paramref name="match"/> was recorded; anything at all when it is <c>null</c>.</summary>
    /// <param name="match">Optional predicate.</param>
    public bool Any(Func<RecordedTransition<TState>, bool>? match = null)
        => match is null ? !_transitions.IsEmpty : _transitions.Any(match);

    /// <summary>
    /// Whether a <typeparamref name="TEvent"/> moved an instance from <paramref name="from"/> to <paramref name="to"/> —
    /// the from-state → event → to-state assertion, with <c>null</c> meaning "any" on every part.
    /// </summary>
    /// <typeparam name="TEvent">The event that drove the transition.</typeparam>
    /// <param name="from">The source state, or <c>null</c> for any. Match a <i>created</i> instance with <see cref="SagaStates.Initial"/>.</param>
    /// <param name="to">The target state, or <c>null</c> for any.</param>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for any.</param>
    public bool Has<TEvent>(string? from = null, string? to = null, string? correlationId = null)
        where TEvent : class, IEvent
        => _transitions.Any(Matcher<TEvent>(from, to, correlationId));

    /// <summary>The records matching a from-state → event → to-state edge.</summary>
    /// <typeparam name="TEvent">The event that drove the transition.</typeparam>
    /// <param name="from">The source state, or <c>null</c> for any.</param>
    /// <param name="to">The target state, or <c>null</c> for any.</param>
    /// <param name="correlationId">Narrow to one instance, or <c>null</c> for any.</param>
    public IReadOnlyList<RecordedTransition<TState>> Of<TEvent>(string? from = null, string? to = null, string? correlationId = null)
        where TEvent : class, IEvent
        => [.. _transitions.Where(Matcher<TEvent>(from, to, correlationId))];

    /// <summary>Wait until <paramref name="count"/> records match <paramref name="match"/>, then return them.</summary>
    /// <param name="match">The predicate.</param>
    /// <param name="count">How many matches to wait for. Default 1.</param>
    /// <param name="what">What the caller was waiting for, for the timeout message. Defaults to "the predicate".</param>
    /// <param name="timeout">Overall budget. Defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">The budget elapsed with fewer than <paramref name="count"/> matches.</exception>
    public async Task<IReadOnlyList<RecordedTransition<TState>>> WaitForAsync(
        Func<RecordedTransition<TState>, bool> match,
        int count = 1,
        string? what = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var budget = timeout ?? DefaultTimeout;
        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Captured BEFORE the snapshot: an append landing between the two completes this source, so the next
            // iteration sees it instead of parking on a signal that already fired.
            Task appended;
            lock (_sync)
                appended = _appended.Task;

            var matches = _transitions.Where(match).ToArray();
            if (matches.Length >= count)
                return matches;

            var remaining = budget - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException($"Timed out after {budget} waiting for {count} saga transition(s) matching {what ?? "the predicate"}; saw {matches.Length}. {this}");

            try
            {
                await appended.WaitAsync(remaining, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Budget spent — loop once more so the throw carries the census rather than the bare framework message.
            }
        }
    }

    /// <summary>Drop everything recorded so far. Waiters already parked are left to time out on their own budget.</summary>
    public void Clear() => _transitions.Clear();

    /// <summary>A one-line census of the edges taken — the payload of a timeout message.</summary>
    public override string ToString()
    {
        if (_transitions.IsEmpty)
            return "Transitions: none";

        var groups = _transitions
            .GroupBy(static transition => transition.ToString(), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => $"{group.Key} x{group.Count()}");

        return $"Transitions: {string.Join(", ", groups)}";
    }

    internal void Append(RecordedTransition<TState> transition)
    {
        _transitions.Enqueue(transition);

        TaskCompletionSource appended;
        lock (_sync)
        {
            // Swapped before completion so a waiter arriving mid-append registers on the NEXT signal rather than on a
            // source that is about to complete for an append it already saw.
            appended = _appended;
            _appended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        appended.TrySetResult(); // outside the lock — continuations must never run under it
    }

    private static Func<RecordedTransition<TState>, bool> Matcher<TEvent>(string? from, string? to, string? correlationId)
        where TEvent : class, IEvent
        => transition => transition.Is<TEvent>()
            && (from is null || string.Equals(transition.FromState, from, StringComparison.Ordinal))
            && (to is null || string.Equals(transition.ToState, to, StringComparison.Ordinal))
            && (correlationId is null || string.Equals(transition.CorrelationId, correlationId, StringComparison.Ordinal));
}

/// <summary>A saga timeout the harness saw go out on the wire, with the headers that decide whether the instance will still accept it.</summary>
/// <remarks>
/// Projected from <see cref="MessagingTestHarness.Published"/> rather than recorded separately: a timeout <i>is</i> an
/// ordinary published event, and the publish is what proves the transport has already parked it — which is what makes
/// advancing a fake clock deterministic instead of a race against the schedule.
/// </remarks>
public sealed record RecordedSagaTimeout
{
    /// <summary>The timeout's declared name — the key its token lives under in <see cref="ISagaState.TimeoutTokens"/>, and what <c>Unschedule</c> cancels.</summary>
    public required string Name { get; init; }

    /// <summary>The token minted when it was scheduled. An arriving timeout whose token no longer matches the instance's is dropped as stale.</summary>
    public required string Token { get; init; }

    /// <summary>The instance the timeout is addressed to.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>When the transport may deliver it; <c>null</c> on a transport that carries no delivery time.</summary>
    public required DateTimeOffset? DueUtc { get; init; }

    /// <summary>The published envelope.</summary>
    public required EventEnvelope Envelope { get; init; }

    /// <summary>The transport message id — the same id the delivery is recorded under, because the scheduler parks and re-enqueues the one envelope.</summary>
    public string MessageId => Envelope.MessageId;

    /// <summary>The timeout event payload.</summary>
    public object Event => Envelope.Body;

    /// <summary>Runtime type of <see cref="Event"/>.</summary>
    public Type EventType => Envelope.BodyType;

    /// <summary>The timeout as <c>name@due</c>.</summary>
    public override string ToString() => $"{Name}@{DueUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "immediate"}";
}
