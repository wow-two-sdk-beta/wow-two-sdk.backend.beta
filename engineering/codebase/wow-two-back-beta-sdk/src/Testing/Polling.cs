namespace WoW.Two.Sdk.Backend.Beta.Testing;

/// <summary>
/// Retry-with-timeout helpers for asserting on eventually-consistent state — e.g. an async write
/// that lands a moment after the request returns (outbox flush, background projection, cache fill).
/// </summary>
/// <remarks>
/// Host-agnostic: the caller supplies the probe, so this works against any client, store, or fixture.
/// Polls real wall-clock time; it is NOT driven by <c>FakeTimeProvider</c>.
/// </remarks>
public static class Polling
{
    /// <summary>Default overall budget before <see cref="UntilAsync{T}"/> gives up and returns the last value.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Default delay between probe attempts.</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Polls <paramref name="probe"/> until <paramref name="predicate"/> holds or the timeout elapses,
    /// returning the last probed value (so a failed assertion can inspect what was actually seen).
    /// </summary>
    /// <typeparam name="T">The probed value type.</typeparam>
    /// <param name="probe">Reads the current value (e.g. a GET, a row count). Awaited on every attempt.</param>
    /// <param name="predicate">The success condition; polling stops the first time it returns <c>true</c>.</param>
    /// <param name="timeout">Overall budget. Defaults to <see cref="DefaultTimeout"/>.</param>
    /// <param name="interval">Delay between attempts. Defaults to <see cref="DefaultInterval"/>.</param>
    /// <param name="cancellationToken">Cancels the wait between probes.</param>
    /// <returns>The value from the last probe — satisfying <paramref name="predicate"/> on success, otherwise the final attempt before timeout.</returns>
    public static async Task<T> UntilAsync<T>(
        Func<Task<T>> probe,
        Func<T, bool> predicate,
        TimeSpan? timeout = null,
        TimeSpan? interval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(predicate);

        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        var step = interval ?? DefaultInterval;

        var value = await probe().ConfigureAwait(false);
        while (!predicate(value) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(step, cancellationToken).ConfigureAwait(false);
            value = await probe().ConfigureAwait(false);
        }

        return value;
    }
}
