namespace WoW.Two.Sdk.Backend.Beta.Web.Quotas;

/// <summary>Defines the counters quotas keep; every change is atomic across callers sharing the store.</summary>
public interface IQuotaRepository
{
    /// <summary>
    /// Adds <paramref name="amount"/> unless that passes <paramref name="limit"/>, and returns the new count; returns
    /// null, changing nothing, when it would pass. A first write expires at <paramref name="expiresAt"/>.
    /// </summary>
    /// <param name="key">The counter.</param>
    /// <param name="amount">The units to add.</param>
    /// <param name="limit">The most units the counter may hold; null is unlimited.</param>
    /// <param name="expiresAt">When a new counter expires; null never.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long?> TryIncrementAsync(string key, long amount, long? limit, DateTimeOffset? expiresAt, CancellationToken cancellationToken = default);

    /// <summary>Takes <paramref name="amount"/> back, never below zero.</summary>
    /// <param name="key">The counter.</param>
    /// <param name="amount">The units to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DecrementAsync(string key, long amount, CancellationToken cancellationToken = default);

    /// <summary>The counter's value; zero when it does not exist or expired.</summary>
    /// <param name="key">The counter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<long> GetAsync(string key, CancellationToken cancellationToken = default);
}
