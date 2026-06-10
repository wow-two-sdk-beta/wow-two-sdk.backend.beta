using System.Collections.Concurrent;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>
/// In-memory <see cref="IOtpStore"/> — dev / test / single-instance only. Register as a singleton
/// so records survive across requests. Multi-instance deployments need a shared store
/// (Postgres, Redis) or verification will fail on the instance that didn't create the code.
/// </summary>
public sealed class MemoryOtpStore : IOtpStore
{
    private static readonly TimeSpan PruneGrace = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<Guid, OtpRecord> _records = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the store over a time source.</summary>
    /// <param name="timeProvider">Time source for rate-limit and prune checks.</param>
    public MemoryOtpStore(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<bool> HasRecentPendingAsync(string subject, string scope, TimeSpan window, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var cutoff = _timeProvider.GetUtcNow() - window;
        var hasRecent = _records.Values.Any(r => !r.Consumed && r.CreatedAt >= cutoff && Matches(r, subject, scope));
        return Task.FromResult(hasRecent);
    }

    /// <inheritdoc />
    public Task SaveAsync(OtpRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        Prune();
        _records[record.Id] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<OtpRecord?> FindLatestPendingAsync(string subject, string scope, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        var latest = _records.Values
            .Where(r => !r.Consumed && Matches(r, subject, scope))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefault();
        return Task.FromResult(latest);
    }

    /// <inheritdoc />
    public Task IncrementAttemptsAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        Mutate(recordId, r => r with { Attempts = r.Attempts + 1 });
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkConsumedAsync(Guid recordId, CancellationToken cancellationToken = default)
    {
        Mutate(recordId, r => r with { Consumed = true });
        return Task.CompletedTask;
    }

    private static bool Matches(OtpRecord record, string subject, string scope)
        => string.Equals(record.Subject, subject, StringComparison.Ordinal)
           && string.Equals(record.Scope, scope, StringComparison.Ordinal);

    private void Mutate(Guid recordId, Func<OtpRecord, OtpRecord> update)
    {
        while (_records.TryGetValue(recordId, out var current))
        {
            if (_records.TryUpdate(recordId, update(current), current))
            {
                return;
            }
        }
    }

    private void Prune()
    {
        var horizon = _timeProvider.GetUtcNow() - PruneGrace;
        foreach (var entry in _records)
        {
            if (entry.Value.ExpiresAt < horizon)
            {
                _records.TryRemove(entry.Key, out _);
            }
        }
    }
}
