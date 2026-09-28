namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Accesses quota counters in process memory — one host only; use <see cref="RedisQuotaRepository"/> across hosts.</summary>
/// <param name="time">The clock expiry is measured against.</param>
public sealed class InMemoryQuotaRepository(TimeProvider time) : IQuotaRepository
{
    private readonly Dictionary<string, (long Value, DateTimeOffset? ExpiresAt)> _counters = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public Task<long?> TryIncrementAsync(string key, long amount, long? limit, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            var (value, expiry) = Current(key) ?? (0, expiresAt);
            if (limit is { } cap && value + amount > cap)
                return Task.FromResult<long?>(null);

            _counters[key] = (value + amount, expiry);
            return Task.FromResult<long?>(value + amount);
        }
    }

    /// <inheritdoc />
    public Task DecrementAsync(string key, long amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
        {
            if (Current(key) is { } counter)
                _counters[key] = (Math.Max(counter.Value - amount, 0), counter.ExpiresAt);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<long> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        lock (_gate)
            return Task.FromResult(Current(key)?.Value ?? 0);
    }

    /// <summary>The live counter, dropping an expired one.</summary>
    private (long Value, DateTimeOffset? ExpiresAt)? Current(string key)
    {
        if (!_counters.TryGetValue(key, out var counter))
            return null;
        if (counter.ExpiresAt is { } expiry && expiry <= time.GetUtcNow())
        {
            _counters.Remove(key);
            return null;
        }

        return counter;
    }
}
