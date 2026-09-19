using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

/// <summary>Default <see cref="IDeadLetterAdmin"/> — uses an <see cref="IDeadLetterQueryRepository"/> where the registered store is one, and degrades to <see cref="IDeadLetterRepository"/> where it is not.</summary>
internal sealed partial class DeadLetterAdmin : IDeadLetterAdmin
{
    private readonly IDeadLetterRepository _store;
    private readonly IDeadLetterQueryRepository? _queryStore;
    private readonly DeadLetterAdminOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeadLetterAdmin> _logger;

    public DeadLetterAdmin(
        IDeadLetterRepository store,
        DeadLetterAdminOptions options,
        TimeProvider timeProvider,
        ILogger<DeadLetterAdmin> logger)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _queryStore = store as IDeadLetterQueryRepository;
        _options = options;
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
            // The store filters natively; re-testing here would double-apply the predicate.
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
            // ReadAsync takes one source, so browsing with none named throws instead of reading as an empty DLQ.
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
            Envelope = DeadLetterHeaderConstants.StampRedrive(record.Envelope, redrives + 1, now),
        };

        try
        {
            // Stamp the marker before replaying — a failed write means the record is gone, so report NotFound.
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

        // Materialize before the first redrive — a redrive removes the record it replays.
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
                failures.Add(new DeadLetterRedriveFailure { MessageId = record.MessageId, Outcome = outcome });
        }

        LogBulkRedrive(redriven, matched.Count);
        return new DeadLetterRedriveResult { Matched = matched.Count, Redriven = redriven, Failures = failures };
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
            // Throw rather than return 0, so a purge that deletes nothing cannot read as an empty DLQ.
            throw new NotSupportedException(
                $"Purging requires the registered {nameof(IDeadLetterRepository)} ({_store.GetType().Name}) to implement {nameof(IDeadLetterQueryRepository)}.");
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

        // Releasing targets records the default query hides, so widen it.
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

        // Fall back to DeadLetterAsync, an upsert keyed by message id; a logging store logs again here.
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

    [LoggerMessage(EventId = 6077, Level = LogLevel.Warning, Message = "Dead-letter store {StoreType} does not implement IDeadLetterQueryRepository; a browse across all sources returns nothing — name the sources on the query or register a query-capable store")]
    private partial void LogCrossSourceBrowseUnsupported(string storeType);

    [LoggerMessage(EventId = 6078, Level = LogLevel.Warning, Message = "Dead-letter store {StoreType} does not implement IDeadLetterQueryRepository; message {MessageId} cannot be resolved by id alone — supply its source")]
    private partial void LogByIdLookupUnsupported(string storeType, string messageId);
}
