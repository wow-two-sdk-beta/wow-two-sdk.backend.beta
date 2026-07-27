using System.Collections.Concurrent;
using System.Diagnostics;
using WoW.Two.Sdk.Backend.Beta.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// One message as the harness saw it at a single point on the pipeline — the envelope plus whatever that point knew
/// (the consume outcome, the exception). Which point it was is the log it landed in, not a field here.
/// </summary>
public sealed record RecordedMessage
{
    /// <summary>The envelope the observer was handed. On the consume side this is the reconstructed envelope, so <see cref="Body"/> is the deserialized event.</summary>
    public required EventEnvelope Envelope { get; init; }

    /// <summary>How the delivery attempt ended. Set only on <see cref="MessagingTestHarness.Consumed"/>; <c>null</c> everywhere else.</summary>
    public ConsumeOutcome? Outcome { get; init; }

    /// <summary>The fault, on the fault logs (<see cref="MessagingTestHarness.Faulted"/>, <see cref="MessagingTestHarness.DeadLettered"/>, <see cref="MessagingTestHarness.PublishFaults"/>); <c>null</c> on the success logs.</summary>
    public Exception? Exception { get; init; }

    /// <summary>Wall-clock time the harness recorded this — for ordering across logs in a failure dump.</summary>
    public required DateTimeOffset RecordedAtUtc { get; init; }

    /// <summary>The transport message id.</summary>
    public string MessageId => Envelope.MessageId;

    /// <summary>The destination (queue/topic) the message was published to or received from.</summary>
    public string Destination => Envelope.Destination;

    /// <summary>The event payload.</summary>
    public object Body => Envelope.Body;

    /// <summary>Runtime type of <see cref="Body"/>.</summary>
    public Type EventType => Envelope.BodyType;

    /// <summary>True when the payload is a <typeparamref name="TEvent"/> (assignability, so a base type or interface matches too).</summary>
    /// <typeparam name="TEvent">The event contract to test for.</typeparam>
    public bool Is<TEvent>()
        where TEvent : class, IEvent
        => Envelope.Body is TEvent;

    /// <summary>The payload as a <typeparamref name="TEvent"/>.</summary>
    /// <typeparam name="TEvent">The event contract to cast to.</typeparam>
    /// <exception cref="InvalidCastException">The payload is not a <typeparamref name="TEvent"/>.</exception>
    public TEvent BodyAs<TEvent>()
        where TEvent : class, IEvent
        => Envelope.Body as TEvent ?? throw new InvalidCastException($"Message '{MessageId}' carries a {EventType.Name}, not a {typeof(TEvent).Name}.");
}

/// <summary>
/// One phase's worth of <see cref="RecordedMessage"/>s — append-only, thread-safe, and awaitable: a test asserts on
/// what is already there, or awaits what has not arrived yet.
/// </summary>
/// <remarks>
/// The waits are signal-driven, not polled: each append completes the pending waiters, so a test blocks exactly as
/// long as the message takes rather than for a fixed sleep. A wait that runs out of budget throws a
/// <see cref="TimeoutException"/> naming what it wanted and dumping what it actually saw — a bare
/// <c>false</c> makes for a useless assertion failure.
/// </remarks>
public sealed class RecordedMessageLog
{
    /// <summary>Overall budget for a wait that does not pass its own timeout.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<RecordedMessage> _messages = new();
    private readonly Lock _sync = new();
    private TaskCompletionSource _appended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal RecordedMessageLog(string name) => Name = name;

    /// <summary>The pipeline phase this log covers (<c>Published</c>, <c>Consumed</c>, …). Used in timeout messages.</summary>
    public string Name { get; }

    /// <summary>Everything recorded so far, in arrival order. A snapshot — later appends do not mutate it.</summary>
    public IReadOnlyList<RecordedMessage> All => [.. _messages];

    /// <summary>How many messages matching <paramref name="match"/> were recorded; every message when it is <c>null</c>.</summary>
    /// <param name="match">Optional record predicate.</param>
    public int Count(Func<RecordedMessage, bool>? match = null)
        => match is null ? _messages.Count : _messages.Count(match);

    /// <summary>How many <typeparamref name="TEvent"/> messages were recorded, optionally narrowed by payload.</summary>
    /// <typeparam name="TEvent">The event contract to filter on.</typeparam>
    /// <param name="match">Optional payload predicate.</param>
    public int Count<TEvent>(Func<TEvent, bool>? match = null)
        where TEvent : class, IEvent
        => _messages.Count(Matcher(match));

    /// <summary>Whether anything matching <paramref name="match"/> was recorded; anything at all when it is <c>null</c>.</summary>
    /// <param name="match">Optional record predicate.</param>
    public bool Any(Func<RecordedMessage, bool>? match = null)
        => match is null ? !_messages.IsEmpty : _messages.Any(match);

    /// <summary>Whether a <typeparamref name="TEvent"/> matching <paramref name="match"/> was recorded.</summary>
    /// <typeparam name="TEvent">The event contract to filter on.</typeparam>
    /// <param name="match">Optional payload predicate.</param>
    public bool Any<TEvent>(Func<TEvent, bool>? match = null)
        where TEvent : class, IEvent
        => _messages.Any(Matcher(match));

    /// <summary>The recorded <typeparamref name="TEvent"/> messages, optionally narrowed by payload.</summary>
    /// <typeparam name="TEvent">The event contract to filter on.</typeparam>
    /// <param name="match">Optional payload predicate.</param>
    public IReadOnlyList<RecordedMessage> Of<TEvent>(Func<TEvent, bool>? match = null)
        where TEvent : class, IEvent
        => [.. _messages.Where(Matcher(match))];

    /// <summary>The recorded <typeparamref name="TEvent"/> payloads — for asserting on event content rather than on envelopes.</summary>
    /// <typeparam name="TEvent">The event contract to filter on.</typeparam>
    /// <param name="match">Optional payload predicate.</param>
    public IReadOnlyList<TEvent> Bodies<TEvent>(Func<TEvent, bool>? match = null)
        where TEvent : class, IEvent
        => [.. _messages.Where(Matcher(match)).Select(static message => (TEvent)message.Body)];

    /// <summary>
    /// Wait until <paramref name="count"/> <typeparamref name="TEvent"/> messages matching <paramref name="match"/>
    /// have been recorded, then return them.
    /// </summary>
    /// <typeparam name="TEvent">The event contract to wait for.</typeparam>
    /// <param name="count">How many matches to wait for. Default 1.</param>
    /// <param name="match">Optional payload predicate.</param>
    /// <param name="timeout">Overall budget. Defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">The budget elapsed with fewer than <paramref name="count"/> matches.</exception>
    public Task<IReadOnlyList<RecordedMessage>> WaitForAsync<TEvent>(
        int count = 1,
        Func<TEvent, bool>? match = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
        => WaitCoreAsync(Matcher(match), count, typeof(TEvent).Name, timeout, cancellationToken);

    /// <summary>Wait until <paramref name="count"/> messages matching <paramref name="match"/> have been recorded, then return them.</summary>
    /// <param name="match">Record predicate — the escape hatch for asserting on envelope fields, outcomes, or exceptions.</param>
    /// <param name="count">How many matches to wait for. Default 1.</param>
    /// <param name="timeout">Overall budget. Defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="TimeoutException">The budget elapsed with fewer than <paramref name="count"/> matches.</exception>
    public Task<IReadOnlyList<RecordedMessage>> WaitForAsync(
        Func<RecordedMessage, bool> match,
        int count = 1,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        return WaitCoreAsync(match, count, "the predicate", timeout, cancellationToken);
    }

    /// <summary>Drop everything recorded so far. Waiters already parked are left to time out on their own budget.</summary>
    public void Clear() => _messages.Clear();

    /// <summary>A one-line census of the log — the payload of a timeout message.</summary>
    public override string ToString()
    {
        if (_messages.IsEmpty)
            return $"{Name}: none";

        var groups = _messages
            .GroupBy(static message => message.Outcome is { } outcome ? $"{message.EventType.Name}/{outcome}" : message.EventType.Name)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => $"{group.Key} x{group.Count()}");

        return $"{Name}: {string.Join(", ", groups)}";
    }

    internal void Append(RecordedMessage message)
    {
        _messages.Enqueue(message);

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

    private async Task<IReadOnlyList<RecordedMessage>> WaitCoreAsync(
        Func<RecordedMessage, bool> match,
        int count,
        string what,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
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

            var matches = _messages.Where(match).ToArray();
            if (matches.Length >= count)
                return matches;

            var remaining = budget - Stopwatch.GetElapsedTime(started);
            if (remaining <= TimeSpan.Zero)
                throw new TimeoutException($"Timed out after {budget} waiting for {count} {Name} message(s) matching {what}; saw {matches.Length}. {this}");

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

    private static Func<RecordedMessage, bool> Matcher<TEvent>(Func<TEvent, bool>? match)
        where TEvent : class, IEvent
    {
        if (match is null)
            return static message => message.Body is TEvent;

        return message => message.Body is TEvent body && match(body);
    }
}
