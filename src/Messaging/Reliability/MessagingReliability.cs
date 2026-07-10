namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Backoff curve applied between retry attempts.</summary>
public enum BackoffKind
{
    /// <summary>No delay — retry immediately.</summary>
    None,

    /// <summary>Constant delay equal to the base delay.</summary>
    Fixed,

    /// <summary>Exponential growth: <c>base × 2^(attempt-1)</c>, capped at the max delay.</summary>
    Exponential,

    /// <summary>Exponential growth with random jitter (recommended default for distributed work).</summary>
    ExponentialJitter,
}

/// <summary>Retry schedule configuration.</summary>
/// <param name="MaxAttempts">Maximum delivery attempts before the message is dead-lettered.</param>
/// <param name="Backoff">Backoff curve between attempts.</param>
/// <param name="BaseDelay">Base delay; defaults to 200ms when null.</param>
/// <param name="MaxDelay">Delay ceiling; defaults to 30s when null.</param>
public sealed record RetryConfig(
    int MaxAttempts = 5,
    BackoffKind Backoff = BackoffKind.ExponentialJitter,
    TimeSpan? BaseDelay = null,
    TimeSpan? MaxDelay = null);

/// <summary>Computes the delay before the next retry attempt, or <c>null</c> when attempts are exhausted.</summary>
public interface IRetryPolicy
{
    /// <summary>Delay before <paramref name="attempt"/> (1-based, after the first failure), or <c>null</c> to stop retrying.</summary>
    /// <param name="attempt">The upcoming attempt number (1 = first retry).</param>
    /// <param name="config">The retry configuration.</param>
    TimeSpan? NextDelay(int attempt, RetryConfig config);
}

/// <summary>A message that exhausted retries (or was rejected as non-retryable) and was moved aside.</summary>
/// <param name="MessageId">The dead-lettered message id.</param>
/// <param name="Destination">The source destination/queue.</param>
/// <param name="Reason">Human-readable failure reason.</param>
/// <param name="ExceptionType">Full name of the terminal exception type, if any.</param>
/// <param name="Envelope">The original envelope, retained for replay.</param>
/// <param name="DeadLetteredAtUtc">When the message was dead-lettered.</param>
public sealed record DeadLetterRecord(
    string MessageId,
    string Destination,
    string Reason,
    string? ExceptionType,
    EventEnvelope Envelope,
    DateTimeOffset DeadLetteredAtUtc)
{
    /// <summary>Build a record from a failed delivery.</summary>
    /// <param name="envelope">The envelope being dead-lettered.</param>
    /// <param name="exception">The terminal exception.</param>
    /// <param name="deadLetteredAtUtc">The current UTC time.</param>
    public static DeadLetterRecord From(EventEnvelope envelope, Exception exception, DateTimeOffset deadLetteredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(exception);
        return new DeadLetterRecord(envelope.MessageId, envelope.Destination, exception.Message, exception.GetType().FullName, envelope, deadLetteredAtUtc);
    }
}

/// <summary>Poison-message terminus with replay (redrive). Native broker DLQs back this on adapters; in-memory by default.</summary>
public interface IDeadLetterStore
{
    /// <summary>Move a message into the dead-letter store.</summary>
    /// <param name="record">The dead-letter record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask DeadLetterAsync(DeadLetterRecord record, CancellationToken cancellationToken);

    /// <summary>Enumerate dead-lettered messages for a source destination.</summary>
    /// <param name="source">The source destination/queue name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<DeadLetterRecord> ReadAsync(string source, CancellationToken cancellationToken);

    /// <summary>Replay (redrive) a dead-lettered message back to its source with a reset delivery count.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ReplayAsync(string messageId, CancellationToken cancellationToken);
}

/// <summary>
/// Idempotency seam — processes an event exactly-once. Dedupes by message id and, for durable impls, runs the
/// handler in the <b>same transaction</b> as the dedupe mark, so both commit or neither (closing the
/// mark-then-crash window). Returns <c>false</c> if already processed (skip).
/// </summary>
public interface IInboxProcessor
{
    /// <summary>Run <paramref name="handler"/> exactly once for <paramref name="messageId"/>; returns <c>false</c> if it was already processed.</summary>
    /// <param name="messageId">The message id (dedupe key).</param>
    /// <param name="handler">The processing action (dispatch); executed at most once per successful commit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> ProcessOnceAsync(string messageId, Func<CancellationToken, ValueTask> handler, CancellationToken cancellationToken);
}

/// <summary>Schedules an envelope for delayed (future) delivery.</summary>
public interface IEventScheduler
{
    /// <summary>Schedule an envelope to become deliverable at or after <paramref name="notBeforeUtc"/>.</summary>
    /// <param name="envelope">The envelope to schedule.</param>
    /// <param name="notBeforeUtc">Earliest delivery time (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ScheduleAsync(EventEnvelope envelope, DateTimeOffset notBeforeUtc, CancellationToken cancellationToken);
}

/// <summary>A staged outgoing message in a transactional outbox.</summary>
/// <param name="Id">Outbox row id.</param>
/// <param name="Type">Logical message type/name.</param>
/// <param name="Payload">Serialized message body.</param>
/// <param name="OccurredOnUtc">When the message was produced.</param>
/// <param name="Headers">Headers to attach on dispatch.</param>
public sealed record OutboxRecord(
    string Id,
    string Type,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset OccurredOnUtc,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>Transactional outbox port — enrolls an outgoing message in the ambient DB transaction (EF / CAP adapters implement this).</summary>
public interface IOutbox
{
    /// <summary>Stage a message for reliable publish within the current transaction.</summary>
    /// <param name="record">The outbox record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask EnqueueAsync(OutboxRecord record, CancellationToken cancellationToken);
}

/// <summary>Drains staged outbox messages to the transport. Driven by a hosted service or the backing engine.</summary>
public interface IOutboxDispatcher
{
    /// <summary>Dispatch up to <paramref name="batchSize"/> pending outbox messages; returns the number dispatched.</summary>
    /// <param name="batchSize">Maximum messages to dispatch in this pass.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> DispatchAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>Prune processed (dispatched or given-up) rows older than <paramref name="retention"/>; returns rows removed. No-op by default.</summary>
    /// <param name="retention">How long to retain processed rows before pruning.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> PruneProcessedAsync(TimeSpan retention, CancellationToken cancellationToken) => ValueTask.FromResult(0);
}

/// <summary>Default <see cref="IRetryPolicy"/> — fixed / exponential / exponential-with-jitter backoff, dependency-free.</summary>
public sealed class DefaultRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public TimeSpan? NextDelay(int attempt, RetryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (attempt < 1 || attempt >= config.MaxAttempts)
            return null;

        var baseDelay = config.BaseDelay ?? DefaultBaseDelay;
        var maxDelay = config.MaxDelay ?? DefaultMaxDelay;

        var delay = config.Backoff switch
        {
            BackoffKind.None => TimeSpan.Zero,
            BackoffKind.Fixed => baseDelay,
            BackoffKind.Exponential => ScaleExponential(baseDelay, attempt),
            BackoffKind.ExponentialJitter => ApplyJitter(ScaleExponential(baseDelay, attempt)),
            _ => baseDelay,
        };

        return delay > maxDelay ? maxDelay : delay;
    }

    private static TimeSpan ScaleExponential(TimeSpan baseDelay, int attempt)
    {
        var factor = Math.Min(Math.Pow(2, attempt - 1), 1_000_000d);
        return TimeSpan.FromTicks((long)(baseDelay.Ticks * factor));
    }

    private static TimeSpan ApplyJitter(TimeSpan delay)
    {
        var multiplier = (Random.Shared.NextDouble() * 0.5) + 0.75; // 0.75x – 1.25x
        return TimeSpan.FromTicks((long)(delay.Ticks * multiplier));
    }
}
