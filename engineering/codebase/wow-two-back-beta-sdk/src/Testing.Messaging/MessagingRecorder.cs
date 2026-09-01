using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// The harness's eyes on the bus — one observer implementing all three seams
/// (<see cref="IPublishObservingInterceptor"/>, <see cref="IReceiveObservingInterceptor"/>, <see cref="IConsumeObservingInterceptor"/>), recording every
/// hook into a <see cref="RecordedMessageLog"/> and stamping the activity clock that idle detection reads.
/// </summary>
/// <remarks>
///   - an observer cannot short-circuit, re-settle, or swallow a fault
///   - the log phases are not interchangeable (phase contract in <c>Testing.Messaging.md</c>)
///   - every member is thread-safe
/// </remarks>
public sealed class MessagingRecorder : IPublishObservingInterceptor, IReceiveObservingInterceptor, IConsumeObservingInterceptor
{
    private readonly Lock _sync = new();
    private TaskCompletionSource _activity = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _lastActivity = Stopwatch.GetTimestamp();

    /// <summary>Envelopes the transport accepted (<see cref="IPublishObservingInterceptor.PostPublishAsync"/>). What acceptance proves is transport-specific — see <see cref="ITransportCapabilities.NativePublisherConfirms"/>.</summary>
    public RecordedMessageLog Published { get; } = new(nameof(Published));

    /// <summary>Sends the transport threw on (<see cref="IPublishObservingInterceptor.PublishFaultAsync"/>). The exception still propagated to the caller.</summary>
    public RecordedMessageLog PublishFaults { get; } = new(nameof(PublishFaults));

    /// <summary>Delivery attempts that completed without throwing, each carrying its <see cref="ConsumeOutcome"/> (<see cref="IConsumeObservingInterceptor.PostConsumeAsync"/>). A deduplicated message records as <see cref="ConsumeOutcome.Duplicate"/>, never as a missing entry.</summary>
    public RecordedMessageLog Consumed { get; } = new(nameof(Consumed));

    /// <summary>Delivery attempts that threw (<see cref="IConsumeObservingInterceptor.ConsumeFaultAsync"/>) — one entry per attempt, so the count is the retry budget spent.</summary>
    public RecordedMessageLog Faulted { get; } = new(nameof(Faulted));

    /// <summary>Messages that exhausted processing and were dead-lettered (<see cref="IReceiveObservingInterceptor.ReceiveFaultAsync"/>), recorded after settlement.</summary>
    public RecordedMessageLog DeadLettered { get; } = new(nameof(DeadLettered));

    /// <summary>
    /// How long since the bus last did anything the recorder can see — a publish, an arrival, an attempt, a
    /// settlement. The quantity <see cref="MessagingTestHarness.WaitForIdleAsync"/> thresholds on.
    /// </summary>
    /// <remarks>Monotonic clock, never <c>TimeProvider</c> — faked time still gets a real quiet window.</remarks>
    public TimeSpan SinceLastActivity => Stopwatch.GetElapsedTime(Volatile.Read(ref _lastActivity));

    /// <summary>
    /// A task that completes on the bus's next observable move. Capture it <b>before</b> reading state, so an event
    /// racing the read completes the task instead of being missed.
    /// </summary>
    public Task NextActivityAsync()
    {
        lock (_sync)
            return _activity.Task;
    }

    /// <summary>Empty every log and restart the activity clock — for reusing one harness across phases of a test.</summary>
    public void Reset()
    {
        Published.Clear();
        PublishFaults.Clear();
        Consumed.Clear();
        Faulted.Clear();
        DeadLettered.Clear();
        Volatile.Write(ref _lastActivity, Stopwatch.GetTimestamp());
    }

    /// <summary>A one-line census of all five logs — the payload of a harness timeout message.</summary>
    public override string ToString() => string.Join(" | ", Published, PublishFaults, Consumed, Faulted, DeadLettered);

    /// <inheritdoc />
    public ValueTask PrePublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        // Marked but not logged — counting a publish in progress stops an idle wait in the send/arrival gap.
        MarkActivity();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask PostPublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
        => Record(Published, envelope, outcome: null, exception: null);

    /// <inheritdoc />
    public ValueTask PublishFaultAsync(EventEnvelope envelope, Exception exception, CancellationToken cancellationToken)
        => Record(PublishFaults, envelope, outcome: null, exception);

    /// <inheritdoc />
    public ValueTask PreReceiveAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        MarkActivity(); // an arrival is activity even though nothing has been decided about it yet
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask PostReceiveAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Terminal success, not logged as Consumed — PostConsume already recorded it, and a second entry doubles it.
        MarkActivity();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ReceiveFaultAsync(ReceiveContext context, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Record(DeadLettered, context.Envelope, outcome: null, exception);
    }

    /// <inheritdoc />
    public ValueTask PreConsumeAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        MarkActivity();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask PostConsumeAsync(ReceiveContext context, ConsumeOutcome outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Record(Consumed, context.Envelope, outcome, exception: null);
    }

    /// <inheritdoc />
    public ValueTask ConsumeFaultAsync(ReceiveContext context, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Record(Faulted, context.Envelope, outcome: null, exception);
    }

    private ValueTask Record(RecordedMessageLog log, EventEnvelope envelope, ConsumeOutcome? outcome, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        log.Append(new RecordedMessage
        {
            Envelope = envelope,
            Outcome = outcome,
            Exception = exception,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        });

        MarkActivity();
        return ValueTask.CompletedTask;
    }

    private void MarkActivity()
    {
        Volatile.Write(ref _lastActivity, Stopwatch.GetTimestamp());

        TaskCompletionSource activity;
        lock (_sync)
        {
            // Swapped before completion so a waiter arriving mid-signal registers on the next move, not on this one.
            activity = _activity;
            _activity = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        activity.TrySetResult(); // outside the lock — continuations must never run under it
    }
}
