using System.Collections.Concurrent;
using System.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging.Trackers;

/// <summary>
/// Tracks every saga reaction the harness recorded, in order — append-only, thread-safe, and awaitable: a test asserts on what
/// is already there, or awaits a state the instance has not reached yet.
/// </summary>
/// <typeparam name="TState">The saga state type.</typeparam>
/// <remarks>
///   - each append completes the pending waiters
///   - a test blocks only as long as the saga takes
///   - a wait past its budget throws <see cref="TimeoutException"/>, dumping the edges actually taken
/// </remarks>
public sealed class RecordedTransitionTracker<TState>
    where TState : class, ISagaState
{
    /// <summary>Holds overall budget for a wait that does not pass its own timeout.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<RecordedTransition<TState>> _transitions = new();
    private readonly Lock _sync = new();
    private TaskCompletionSource _appended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal RecordedTransitionTracker()
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
    /// <param name="from">The source state, or <c>null</c> for any. Match a created instance with <see cref="SagaStateConstants.Initial"/>.</param>
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

            // Captured before the snapshot — an append landing between the two completes this source.
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
            // Swap before completing, so a waiter arriving mid-append registers on the next signal.
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
