using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

/// <summary>
/// The harness's eyes on the bus — one observer implementing all three seams
/// (<see cref="IPublishObserver"/>, <see cref="IReceiveObserver"/>, <see cref="IConsumeObserver"/>), recording every
/// hook into a <see cref="RecordedMessageLog"/> and stamping the activity clock that idle detection reads.
/// </summary>
/// <remarks>
/// <para>
/// Observing rather than filtering is deliberate: the SDK guarantees an observer cannot short-circuit the chain,
/// change settlement, or swallow a fault, so a test that watches the bus through this type is watching the same
/// pipeline production runs. Registering it changes nothing about delivery.
/// </para>
/// <para>
/// Which hook feeds which log is the whole contract, and the phases are NOT interchangeable:
/// <see cref="Consumed"/> is per delivery <i>attempt</i> and carries the <see cref="ConsumeOutcome"/>, so a redelivered
/// message appears once per attempt and a deduplicated one appears with <see cref="ConsumeOutcome.Duplicate"/>;
/// <see cref="Faulted"/> is likewise per attempt, so its count is the retry budget actually spent;
/// <see cref="DeadLettered"/> is terminal and fires <i>after</i> settlement, so by the time a wait on it returns the
/// dead-letter store has already been written and can be read without polling.
/// </para>
/// <para>Registered as a singleton and hit from every consume worker — every member is thread-safe.</para>
/// </remarks>
public sealed class MessagingRecorder : IPublishObserver, IReceiveObserver, IConsumeObserver
{
    private readonly Lock _sync = new();
    private TaskCompletionSource _activity = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _lastActivity = Stopwatch.GetTimestamp();

    /// <summary>Envelopes the transport accepted (<see cref="IPublishObserver.PostPublishAsync"/>). What acceptance proves is transport-specific — see <see cref="ITransportCapabilities.NativePublisherConfirms"/>.</summary>
    public RecordedMessageLog Published { get; } = new(nameof(Published));

    /// <summary>Sends the transport threw on (<see cref="IPublishObserver.PublishFaultAsync"/>). The exception still propagated to the caller.</summary>
    public RecordedMessageLog PublishFaults { get; } = new(nameof(PublishFaults));

    /// <summary>Delivery attempts that completed without throwing, each carrying its <see cref="ConsumeOutcome"/> (<see cref="IConsumeObserver.PostConsumeAsync"/>).</summary>
    public RecordedMessageLog Consumed { get; } = new(nameof(Consumed));

    /// <summary>Delivery attempts that threw (<see cref="IConsumeObserver.ConsumeFaultAsync"/>) — one entry per attempt, so the count is the retry budget spent.</summary>
    public RecordedMessageLog Faulted { get; } = new(nameof(Faulted));

    /// <summary>Messages that exhausted processing and were dead-lettered (<see cref="IReceiveObserver.ReceiveFaultAsync"/>), recorded after settlement.</summary>
    public RecordedMessageLog DeadLettered { get; } = new(nameof(DeadLettered));

    /// <summary>
    /// How long since the bus last did anything the recorder can see — a publish, an arrival, an attempt, a
    /// settlement. The quantity <see cref="MessagingTestHarness.WaitForIdleAsync"/> thresholds on.
    /// </summary>
    /// <remarks>Measured on the monotonic clock, never on <c>TimeProvider</c>: a test that fakes time must still get a real quiet window.</remarks>
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
        // Marked but not logged: a publish in progress is activity, and counting it here is what stops an idle wait
        // returning in the gap between the send and the arrival.
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

        // Terminal success. Not logged as Consumed — that log is per attempt and PostConsume already recorded this
        // message's outcome; double-recording would make a single delivery look like two.
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

/// <summary>DI registration for the messaging test harness's observer.</summary>
public static class MessagingTestingServiceCollectionExtensions
{
    /// <summary>
    /// Register a <see cref="MessagingRecorder"/> on the bus. Use this to watch a host the test did not build — a
    /// <c>WebApplicationFactory</c>, a broker-backed host — then hand its <c>IServiceProvider</c> to
    /// <see cref="MessagingTestHarness.Attach"/>. <see cref="MessagingTestHarness.StartAsync"/> already does this.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddMessagingRecorder(this IServiceCollection services)
        => services.AddMessagingRecorder(new MessagingRecorder());

    /// <summary>Register a specific <see cref="MessagingRecorder"/> instance — for holding a reference to it before the host exists.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="recorder">The recorder to register.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>Idempotent: a second call with a recorder already registered is a no-op, because registering the observer twice would notify it twice and double every count.</remarks>
    public static IServiceCollection AddMessagingRecorder(this IServiceCollection services, MessagingRecorder recorder)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(recorder);

        if (services.Any(static descriptor => descriptor.ServiceType == typeof(MessagingRecorder)))
            return services;

        // Registered BEFORE AddMessageObserver so its own TryAddSingleton<MessagingRecorder>() is the no-op and every
        // observer facet resolves to this instance — the one the caller kept a reference to.
        services.TryAddSingleton(recorder);
        services.AddMessageObserver<MessagingRecorder>();
        return services;
    }
}
