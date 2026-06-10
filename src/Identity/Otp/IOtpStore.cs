namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>
/// Persistence seam for OTP records. The default is the in-memory store (dev / single instance);
/// production multi-instance deployments supply a shared-storage implementation (Postgres, Redis, …).
/// "Pending" means not yet consumed — expiry is judged by <see cref="IOtpService"/>, not the store.
/// </summary>
public interface IOtpStore
{
    /// <summary>Whether a pending code for <c>(subject, scope)</c> was created within <paramref name="window"/> (rate limiting).</summary>
    /// <param name="subject">Identity key.</param>
    /// <param name="scope">Scope key.</param>
    /// <param name="window">Look-back window.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> HasRecentPendingAsync(string subject, string scope, TimeSpan window, CancellationToken cancellationToken = default);

    /// <summary>Persists a new record.</summary>
    /// <param name="record">The record to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveAsync(OtpRecord record, CancellationToken cancellationToken = default);

    /// <summary>Latest pending (unconsumed) record for <c>(subject, scope)</c>, expired or not; <c>null</c> when none.</summary>
    /// <param name="subject">Identity key.</param>
    /// <param name="scope">Scope key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OtpRecord?> FindLatestPendingAsync(string subject, string scope, CancellationToken cancellationToken = default);

    /// <summary>Counts one failed verification attempt against a record.</summary>
    /// <param name="recordId">The record id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IncrementAttemptsAsync(Guid recordId, CancellationToken cancellationToken = default);

    /// <summary>Marks a record consumed so it can never verify again.</summary>
    /// <param name="recordId">The record id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkConsumedAsync(Guid recordId, CancellationToken cancellationToken = default);
}
