using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>
/// Reserved wire headers that carry dead-letter administration state on the envelope itself, so it survives the round
/// trip through the broker and back into the store.
/// </summary>
/// <remarks>
/// They live on the envelope rather than only in the record because the record is rebuilt from scratch every time a
/// message dies: a redriven message that fails again arrives at <see cref="ReceiveContext.DeadLetterAsync"/>, which
/// constructs a new <see cref="DeadLetterRecord"/> knowing nothing about the previous one. The header is what makes the
/// redrive cap hold across replays and restarts.
/// </remarks>
public static class DeadLetterHeaders
{
    /// <summary>Reserved. How many times this message has been redriven out of a dead-letter store.</summary>
    public const string RedriveCount = MessageHeaders.ReservedPrefix + "dl-redrive-count";

    /// <summary>Reserved. Round-trip (<c>o</c>) timestamp of the most recent redrive.</summary>
    public const string RedrivenAt = MessageHeaders.ReservedPrefix + "dl-redriven-at";

    /// <summary>Read the redrive marker off an envelope; 0 when absent or unparseable.</summary>
    /// <param name="envelope">The envelope to inspect.</param>
    public static int ReadRedriveCount(EventEnvelope? envelope)
    {
        if (envelope?.Headers is not { Count: > 0 } headers || !headers.TryGetValue(RedriveCount, out var raw))
            return 0;

        // A malformed marker reads as "never redriven" rather than throwing: this runs on a property getter an operator
        // console calls per row, and a bad header is a reason to under-count, not to fail the browse.
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0 ? count : 0;
    }

    /// <summary>Return a copy of <paramref name="envelope"/> stamped as redriven — marker bumped, delivery count and delay reset.</summary>
    /// <param name="envelope">The envelope being redriven.</param>
    /// <param name="redriveCount">The new redrive count.</param>
    /// <param name="redrivenAtUtc">When the redrive happened.</param>
    public static EventEnvelope StampRedrive(EventEnvelope envelope, int redriveCount, DateTimeOffset redrivenAtUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var headers = new Dictionary<string, string>(envelope.Headers, StringComparer.Ordinal)
        {
            [RedriveCount] = redriveCount.ToString(CultureInfo.InvariantCulture),
            [RedrivenAt] = redrivenAtUtc.ToString("o", CultureInfo.InvariantCulture),
        };

        return envelope with
        {
            // A redrive is a fresh start for the retry budget — the whole point of putting the message back.
            DeliveryCount = 0,

            // Cleared deliberately: a message dead-lettered while carrying a stale future delivery time would otherwise
            // be re-parked for a window that expired long ago.
            NotBeforeUtc = null,
            Headers = headers,
        };
    }
}

/// <summary>
/// Filter over a dead-letter store — the criteria an operator triages by. Every field is optional and they combine with
/// AND; a default instance matches everything the store holds, capped by <see cref="Limit"/>.
/// </summary>
public sealed record DeadLetterQuery
{
    /// <summary>
    /// Source destinations to search. Empty means every source — which needs an <see cref="IDeadLetterQueryStore"/>,
    /// since the floor <see cref="IDeadLetterStore.ReadAsync"/> enumerates one named source at a time.
    /// </summary>
    public IReadOnlyList<string> Sources { get; init; } = [];

    /// <summary>Only records dead-lettered at or after this instant.</summary>
    public DateTimeOffset? DeadLetteredAfterUtc { get; init; }

    /// <summary>Only records dead-lettered strictly before this instant.</summary>
    public DateTimeOffset? DeadLetteredBeforeUtc { get; init; }

    /// <summary>
    /// Terminal exception type. Matches either the full name (<c>System.TimeoutException</c>) or the simple name
    /// (<c>TimeoutException</c>), so an operator does not have to know the namespace to triage by failure kind.
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>Case-insensitive substring the failure reason must contain.</summary>
    public string? ReasonContains { get; init; }

    /// <summary>Only records redriven at least this many times — for finding messages that keep coming back.</summary>
    public int? MinRedriveCount { get; init; }

    /// <summary>Only records redriven at most this many times.</summary>
    public int? MaxRedriveCount { get; init; }

    /// <summary>Include quarantined records alongside the rest. Ignored when <see cref="QuarantinedOnly"/> is set.</summary>
    public bool IncludeQuarantined { get; init; }

    /// <summary>Match <b>only</b> quarantined records — the review queue.</summary>
    public bool QuarantinedOnly { get; init; }

    /// <summary>Maximum records to return. Defaults to 100; zero or negative means unbounded.</summary>
    public int Limit { get; init; } = 100;

    /// <summary>A query over one source with no other criteria.</summary>
    /// <param name="source">The source destination/queue name.</param>
    public static DeadLetterQuery ForSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return new DeadLetterQuery { Sources = [source] };
    }

    /// <summary>True when <paramref name="record"/> satisfies every criterion set on this query. <see cref="Limit"/> is not applied here — the caller counts.</summary>
    /// <param name="record">The record to test.</param>
    public bool Matches(DeadLetterRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (QuarantinedOnly)
        {
            if (record.State != DeadLetterState.Quarantined)
                return false;
        }
        else if (record.State == DeadLetterState.Quarantined && !IncludeQuarantined)
        {
            return false;
        }

        if (Sources.Count > 0 && !Sources.Contains(record.Destination, StringComparer.Ordinal))
            return false;

        if (DeadLetteredAfterUtc is { } after && record.DeadLetteredAtUtc < after)
            return false;

        if (DeadLetteredBeforeUtc is { } before && record.DeadLetteredAtUtc >= before)
            return false;

        if (ExceptionType is { Length: > 0 } exceptionType && !MatchesExceptionType(record.ExceptionType, exceptionType))
            return false;

        if (ReasonContains is { Length: > 0 } reason && !record.Reason.Contains(reason, StringComparison.OrdinalIgnoreCase))
            return false;

        var redrives = record.EffectiveRedriveCount;
        if (MinRedriveCount is { } min && redrives < min)
            return false;

        return MaxRedriveCount is not { } max || redrives <= max;
    }

    private static bool MatchesExceptionType(string? recorded, string wanted)
    {
        if (recorded is null)
            return false;

        if (string.Equals(recorded, wanted, StringComparison.Ordinal))
            return true;

        // Simple-name match: "TimeoutException" finds "System.TimeoutException". The '.' guard stops "Exception" from
        // matching "MyTimeoutException" on a bare suffix compare.
        return recorded.Length > wanted.Length
            && recorded.EndsWith(wanted, StringComparison.Ordinal)
            && recorded[recorded.Length - wanted.Length - 1] == '.';
    }
}

/// <summary>
/// A dead-letter store that can also be searched, corrected and emptied — everything an operator console needs beyond
/// "park it" and "put it back".
/// </summary>
/// <remarks>
/// Optional: <see cref="IDeadLetterAdmin"/> works over a bare <see cref="IDeadLetterStore"/>, restricted to what that
/// interface can express (browse only within named sources, no by-id lookup, no purge). A broker adapter implements
/// this when its DLQ supports browsing and deleting individual messages; where it cannot, the admin degrades rather
/// than pretending.
/// </remarks>
public interface IDeadLetterQueryStore : IDeadLetterStore
{
    /// <summary>Enumerate records matching <paramref name="query"/>, across every source when it names none.</summary>
    /// <param name="query">The filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<DeadLetterRecord> QueryAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Look a record up by message id without knowing its source; <c>null</c> when the store holds none.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRecord?> FindAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Overwrite a stored record in place (redrive marker, quarantine state); <c>false</c> when it is no longer there.</summary>
    /// <param name="record">The record to store, keyed by its message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> UpdateAsync(DeadLetterRecord record, CancellationToken cancellationToken);

    /// <summary>Delete a record without replaying it; <c>false</c> when it is no longer there.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> RemoveAsync(string messageId, CancellationToken cancellationToken);
}

/// <summary>What happened to a single redrive request.</summary>
public enum RedriveOutcome
{
    /// <summary>Re-published to its original destination with a fresh retry budget.</summary>
    Redriven,

    /// <summary>No record with that message id is in the store.</summary>
    NotFound,

    /// <summary>Already redriven <see cref="DeadLetterAdminOptions.MaxRedrives"/> times — the infinite-redrive guard refused it.</summary>
    LimitReached,

    /// <summary>Quarantined; release it before redriving.</summary>
    Quarantined,

    /// <summary>The store rejected the replay. The record is left in place, so the request can be repeated.</summary>
    Failed,

    /// <summary>The registered store cannot service the request — a by-id operation on a store that is not an <see cref="IDeadLetterQueryStore"/>.</summary>
    NotSupported,
}

/// <summary>One message that a bulk redrive did not put back, and why.</summary>
/// <param name="MessageId">The message that was skipped.</param>
/// <param name="Outcome">Why it was skipped.</param>
/// <param name="Detail">Exception detail when <paramref name="Outcome"/> is <see cref="RedriveOutcome.Failed"/>.</param>
public sealed record DeadLetterRedriveFailure(string MessageId, RedriveOutcome Outcome, string? Detail = null);

/// <summary>Tally of a bulk redrive.</summary>
/// <param name="Matched">Records the query selected.</param>
/// <param name="Redriven">Records actually put back.</param>
/// <param name="Failures">Per-message reasons for the rest; empty when everything matched was redriven.</param>
public sealed record DeadLetterRedriveResult(int Matched, int Redriven, IReadOnlyList<DeadLetterRedriveFailure> Failures)
{
    /// <summary>An empty result — nothing matched, nothing to do.</summary>
    public static DeadLetterRedriveResult Empty { get; } = new(0, 0, []);

    /// <summary>True when every matched record was redriven.</summary>
    public bool IsComplete => Failures.Count == 0;
}

/// <summary>
/// Operator surface over a dead-letter store: browse, peek, redrive (singly and in bulk), quarantine, release, purge.
/// A DLQ is otherwise write-only — this is what turns it into something a human can triage.
/// </summary>
/// <remarks>
/// <para>
/// Everything routes through <see cref="IDeadLetterStore"/>, so it is transport-neutral. Operations that need more than
/// that interface can express (by-id lookup, purge) require the store to implement <see cref="IDeadLetterQueryStore"/>
/// and say so via <see cref="RedriveOutcome.NotSupported"/> or a <see cref="NotSupportedException"/> otherwise.
/// </para>
/// <para>
/// Redrive re-publishes through the store's own <see cref="IDeadLetterStore.ReplayAsync"/> rather than through a
/// transport of its own: the marker is written onto the stored record first, so a store that replays what it holds
/// carries the counter onto the wire without the admin needing a publish path.
/// </para>
/// </remarks>
public interface IDeadLetterAdmin
{
    /// <summary>Enumerate dead-lettered messages matching <paramref name="query"/>, newest first where the store can order.</summary>
    /// <param name="query">The filter; a default instance matches everything up to its limit.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<DeadLetterRecord> BrowseAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Read one record by message id. Requires an <see cref="IDeadLetterQueryStore"/>; returns <c>null</c> otherwise.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRecord?> PeekAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Read one record by source and message id — the form that works on any store.</summary>
    /// <param name="source">The source destination/queue name.</param>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRecord?> PeekAsync(string source, string messageId, CancellationToken cancellationToken);

    /// <summary>Redrive a record already in hand — the browse-then-replay path, and the one form that needs nothing beyond the floor store.</summary>
    /// <param name="record">The record to put back.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<RedriveOutcome> RedriveAsync(DeadLetterRecord record, CancellationToken cancellationToken);

    /// <summary>Redrive by message id. Requires an <see cref="IDeadLetterQueryStore"/> to resolve the id.</summary>
    /// <param name="messageId">The dead-lettered message id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<RedriveOutcome> RedriveAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>Redrive every record matching <paramref name="query"/>, capped by its limit.</summary>
    /// <param name="query">The filter selecting what to put back.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<DeadLetterRedriveResult> RedriveAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Quarantine matching records — hold them back from browse and redrive; returns how many changed state.</summary>
    /// <param name="query">The filter selecting what to hold.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> QuarantineAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Release matching records back to <see cref="DeadLetterState.DeadLettered"/>; returns how many changed state.</summary>
    /// <param name="query">The filter; set <see cref="DeadLetterQuery.QuarantinedOnly"/> to target the held set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<int> ReleaseAsync(DeadLetterQuery query, CancellationToken cancellationToken);

    /// <summary>Delete matching records without replaying them; returns how many were removed.</summary>
    /// <param name="query">The filter selecting what to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="NotSupportedException">The registered store is not an <see cref="IDeadLetterQueryStore"/> and cannot delete.</exception>
    ValueTask<int> PurgeAsync(DeadLetterQuery query, CancellationToken cancellationToken);
}

/// <summary>Options for <see cref="IDeadLetterAdmin"/>.</summary>
public sealed class DeadLetterAdminOptions
{
    /// <summary>
    /// How many times one message may be redriven before the guard refuses it. Default 3; zero or negative disables
    /// redrive entirely, which is a way to run the admin read-only.
    /// </summary>
    /// <remarks>
    /// The bound exists because a redrive returns a message to the same handler that already rejected it: without a cap,
    /// a bulk redrive over an unfixed handler is a loop that re-fills the DLQ as fast as it drains it.
    /// </remarks>
    public int MaxRedrives { get; set; } = 3;

    /// <summary>
    /// Quarantine a record when a redrive is refused for hitting <see cref="MaxRedrives"/>, so the next bulk redrive
    /// skips it instead of re-testing and re-failing it. Default <c>true</c>.
    /// </summary>
    public bool QuarantineAtRedriveLimit { get; set; } = true;
}

/// <summary>Default <see cref="IDeadLetterAdmin"/> — uses an <see cref="IDeadLetterQueryStore"/> where the registered store is one, and degrades to <see cref="IDeadLetterStore"/> where it is not.</summary>
internal sealed partial class DeadLetterAdmin : IDeadLetterAdmin
{
    private readonly IDeadLetterStore _store;
    private readonly IDeadLetterQueryStore? _queryStore;
    private readonly DeadLetterAdminOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeadLetterAdmin> _logger;

    public DeadLetterAdmin(
        IDeadLetterStore store,
        IOptions<DeadLetterAdminOptions> options,
        TimeProvider timeProvider,
        ILogger<DeadLetterAdmin> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _queryStore = store as IDeadLetterQueryStore;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async IAsyncEnumerable<DeadLetterRecord> BrowseAsync(
        DeadLetterQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var remaining = query.Limit > 0 ? query.Limit : int.MaxValue;

        if (_queryStore is not null)
        {
            // The store filters natively — re-testing here would double-apply the predicate, and a durable store's
            // QueryAsync is expected to push the criteria down rather than hand back everything.
            await foreach (var record in _queryStore.QueryAsync(query, cancellationToken))
            {
                if (remaining-- <= 0)
                    yield break;

                yield return record;
            }

            yield break;
        }

        if (query.Sources.Count == 0)
        {
            // ReadAsync takes one source; with none named there is nothing to enumerate. Loud, because an operator who
            // asked to browse everything and silently got nothing would read it as an empty DLQ.
            LogCrossSourceBrowseUnsupported(_store.GetType().Name);
            yield break;
        }

        foreach (var source in query.Sources)
        {
            await foreach (var record in _store.ReadAsync(source, cancellationToken))
            {
                if (!query.Matches(record))
                    continue;

                if (remaining-- <= 0)
                    yield break;

                yield return record;
            }
        }
    }

    public async ValueTask<DeadLetterRecord?> PeekAsync(string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (_queryStore is not null)
            return await _queryStore.FindAsync(messageId, cancellationToken);

        LogByIdLookupUnsupported(_store.GetType().Name, messageId);
        return null;
    }

    public async ValueTask<DeadLetterRecord?> PeekAsync(string source, string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (_queryStore is not null)
        {
            var found = await _queryStore.FindAsync(messageId, cancellationToken);
            return found is not null && string.Equals(found.Destination, source, StringComparison.Ordinal) ? found : null;
        }

        await foreach (var record in _store.ReadAsync(source, cancellationToken))
            if (string.Equals(record.MessageId, messageId, StringComparison.Ordinal))
                return record;

        return null;
    }

    public async ValueTask<RedriveOutcome> RedriveAsync(DeadLetterRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.State == DeadLetterState.Quarantined)
            return RedriveOutcome.Quarantined;

        var redrives = record.EffectiveRedriveCount;
        if (redrives >= _options.MaxRedrives)
        {
            LogRedriveLimitReached(record.MessageId, redrives, _options.MaxRedrives);
            if (_options.QuarantineAtRedriveLimit)
                await SaveAsync(record with { State = DeadLetterState.Quarantined }, cancellationToken);

            return RedriveOutcome.LimitReached;
        }

        var now = _timeProvider.GetUtcNow();
        var stamped = record with
        {
            RedriveCount = redrives + 1,
            LastRedrivenAtUtc = now,
            State = DeadLetterState.DeadLettered,
            Envelope = DeadLetterHeaders.StampRedrive(record.Envelope, redrives + 1, now),
        };

        try
        {
            // Marker first, replay second. ReplayAsync republishes what the store holds, so writing the stamped record
            // back is what puts the counter on the wire — and if the replay throws, the record survives with an
            // incremented count rather than being lost mid-flight.
            // A failed write means the record went between the browse and here (a concurrent purge or redrive): report
            // it as gone rather than replaying a message this call no longer owns.
            if (!await SaveAsync(stamped, cancellationToken))
                return RedriveOutcome.NotFound;

            await _store.ReplayAsync(record.MessageId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogRedriveFailed(exception, record.MessageId, record.Destination);
            return RedriveOutcome.Failed;
        }

        LogRedriven(record.MessageId, record.Destination, redrives + 1, _options.MaxRedrives);
        return RedriveOutcome.Redriven;
    }

    public async ValueTask<RedriveOutcome> RedriveAsync(string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        if (_queryStore is null)
        {
            LogByIdLookupUnsupported(_store.GetType().Name, messageId);
            return RedriveOutcome.NotSupported;
        }

        var record = await _queryStore.FindAsync(messageId, cancellationToken);
        return record is null ? RedriveOutcome.NotFound : await RedriveAsync(record, cancellationToken);
    }

    public async ValueTask<DeadLetterRedriveResult> RedriveAsync(DeadLetterQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Materialized before the first redrive: a redrive removes the record it replays, so redriving while enumerating
        // the store would mutate the collection underneath the enumerator.
        var matched = await CollectAsync(query, cancellationToken);
        if (matched.Count == 0)
            return DeadLetterRedriveResult.Empty;

        var redriven = 0;
        List<DeadLetterRedriveFailure> failures = [];
        foreach (var record in matched)
        {
            var outcome = await RedriveAsync(record, cancellationToken);
            if (outcome == RedriveOutcome.Redriven)
                redriven++;
            else
                failures.Add(new DeadLetterRedriveFailure(record.MessageId, outcome));
        }

        LogBulkRedrive(redriven, matched.Count);
        return new DeadLetterRedriveResult(matched.Count, redriven, failures);
    }

    public ValueTask<int> QuarantineAsync(DeadLetterQuery query, CancellationToken cancellationToken)
        => SetStateAsync(query, DeadLetterState.Quarantined, cancellationToken);

    public ValueTask<int> ReleaseAsync(DeadLetterQuery query, CancellationToken cancellationToken)
        => SetStateAsync(query, DeadLetterState.DeadLettered, cancellationToken);

    public async ValueTask<int> PurgeAsync(DeadLetterQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_queryStore is null)
        {
            // Throwing rather than returning 0: a purge that silently deletes nothing reads as an empty DLQ, and the
            // operator moves on believing the queue is clear.
            throw new NotSupportedException(
                $"Purging requires the registered {nameof(IDeadLetterStore)} ({_store.GetType().Name}) to implement {nameof(IDeadLetterQueryStore)}.");
        }

        var matched = await CollectAsync(query, cancellationToken);
        var purged = 0;
        foreach (var record in matched)
            if (await _queryStore.RemoveAsync(record.MessageId, cancellationToken))
                purged++;

        LogPurged(purged, matched.Count);
        return purged;
    }

    private async ValueTask<int> SetStateAsync(DeadLetterQuery query, DeadLetterState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Releasing targets records the default query hides, so widen it rather than making every caller remember to.
        var effective = state == DeadLetterState.DeadLettered && !query.QuarantinedOnly && !query.IncludeQuarantined
            ? query with { IncludeQuarantined = true }
            : query;

        var changed = 0;
        foreach (var record in await CollectAsync(effective, cancellationToken))
        {
            if (record.State == state)
                continue;

            if (await SaveAsync(record with { State = state }, cancellationToken))
                changed++;
        }

        LogStateChanged(changed, state.ToString());
        return changed;
    }

    private async ValueTask<bool> SaveAsync(DeadLetterRecord record, CancellationToken cancellationToken)
    {
        if (_queryStore is not null)
            return await _queryStore.UpdateAsync(record, cancellationToken);

        // Floor-store fallback: DeadLetterAsync is the only write the interface exposes, and every implementation of it
        // is an upsert keyed by message id. The cost is that a store which logs on dead-letter logs again here.
        await _store.DeadLetterAsync(record, cancellationToken);
        return true;
    }

    private async ValueTask<List<DeadLetterRecord>> CollectAsync(DeadLetterQuery query, CancellationToken cancellationToken)
    {
        var records = new List<DeadLetterRecord>();
        await foreach (var record in BrowseAsync(query, cancellationToken))
            records.Add(record);

        return records;
    }

    [LoggerMessage(EventId = 6071, Level = LogLevel.Information, Message = "Redrove dead-lettered message {MessageId} to {Destination} (redrive {RedriveCount} of {MaxRedrives})")]
    private partial void LogRedriven(string messageId, string destination, int redriveCount, int maxRedrives);

    [LoggerMessage(EventId = 6072, Level = LogLevel.Warning, Message = "Refusing to redrive message {MessageId}: already redriven {RedriveCount} times (limit {MaxRedrives})")]
    private partial void LogRedriveLimitReached(string messageId, int redriveCount, int maxRedrives);

    [LoggerMessage(EventId = 6073, Level = LogLevel.Error, Message = "Redriving message {MessageId} to {Destination} failed; the record is left in the store")]
    private partial void LogRedriveFailed(Exception exception, string messageId, string destination);

    [LoggerMessage(EventId = 6074, Level = LogLevel.Information, Message = "Bulk redrive: {Redriven} of {Matched} matched messages put back")]
    private partial void LogBulkRedrive(int redriven, int matched);

    [LoggerMessage(EventId = 6075, Level = LogLevel.Information, Message = "Moved {Changed} dead-letter records to state {State}")]
    private partial void LogStateChanged(int changed, string state);

    [LoggerMessage(EventId = 6076, Level = LogLevel.Information, Message = "Purged {Purged} of {Matched} matched dead-letter records")]
    private partial void LogPurged(int purged, int matched);

    [LoggerMessage(EventId = 6077, Level = LogLevel.Warning, Message = "Dead-letter store {StoreType} does not implement IDeadLetterQueryStore; a browse across all sources returns nothing — name the sources on the query or register a query-capable store")]
    private partial void LogCrossSourceBrowseUnsupported(string storeType);

    [LoggerMessage(EventId = 6078, Level = LogLevel.Warning, Message = "Dead-letter store {StoreType} does not implement IDeadLetterQueryStore; message {MessageId} cannot be resolved by id alone — supply its source")]
    private partial void LogByIdLookupUnsupported(string storeType, string messageId);
}

/// <summary>DI registration for dead-letter administration.</summary>
public static class DeadLetterAdminServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="IDeadLetterAdmin"/> over whatever <see cref="IDeadLetterStore"/> is registered — browse,
    /// peek, redrive, quarantine, release, purge. Purely additive: nothing on the consume path changes, and the admin
    /// does nothing until something calls it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional overrides — chiefly <see cref="DeadLetterAdminOptions.MaxRedrives"/>.</param>
    /// <remarks>
    /// Order-independent relative to the transport registration: the store is resolved when the admin is first used.
    /// Pair with <see cref="AddInMemoryDeadLetterQueryStore"/> (or a broker store that implements
    /// <see cref="IDeadLetterQueryStore"/>) to get cross-source browse, by-id lookup and purge.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddInMemoryEventBus(typeof(Program).Assembly);
    /// services.AddDeadLetterAdmin(o => o.MaxRedrives = 2);
    /// services.AddInMemoryDeadLetterQueryStore();
    ///
    /// // later, from an admin endpoint
    /// var stuck = admin.BrowseAsync(new DeadLetterQuery { ExceptionType = "TimeoutException", Limit = 50 }, ct);
    /// var result = await admin.RedriveAsync(DeadLetterQuery.ForSource("OrderPlaced"), ct);
    /// </code>
    /// </example>
    public static IServiceCollection AddDeadLetterAdmin(this IServiceCollection services, Action<DeadLetterAdminOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<DeadLetterAdminOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDeadLetterAdmin, DeadLetterAdmin>();
        return services;
    }

    /// <summary>
    /// Replace the dead-letter store with the in-memory <see cref="IDeadLetterQueryStore"/> — same behaviour as the
    /// default in-memory store plus cross-source browse, by-id lookup, in-place update and delete.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <remarks>
    /// Explicitly a replacement, not a <c>TryAdd</c>: it overrides whatever store is registered, including a broker
    /// adapter's. Call it only where the process itself is the dead-letter terminus — a broker with a native DLQ should
    /// keep its own store and implement <see cref="IDeadLetterQueryStore"/> there instead. State is process-local and
    /// does not survive a restart, exactly like the store it replaces.
    /// </remarks>
    public static IServiceCollection AddInMemoryDeadLetterQueryStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<IDeadLetterStore, InMemoryDeadLetterQueryStore>());
        return services;
    }
}
