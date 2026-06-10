namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>
/// Orchestrates one-time-password creation and verification, keyed by <c>(subject, scope)</c>.
/// The subject is any string the consumer keys auth on (phone, email, username) — the SDK has no
/// user model. Creation returns the code; the caller decides delivery (see <see cref="IOtpDeliveryHandler"/>).
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates and stores a new code for <c>(subject, scope)</c>. Fails with
    /// <see cref="OtpFailureReason.RateLimited"/> when a code was created within the configured window.
    /// On success the result carries the code for the caller to deliver.
    /// </summary>
    /// <param name="subject">Consumer-defined identity key (phone, email, username).</param>
    /// <param name="scope">Consumer-defined scope the code is valid for (e.g. <c>"crm"</c>, <c>"login"</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OtpCreationResult> CreateAsync(string subject, string scope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies <paramref name="code"/> against the latest pending code for <c>(subject, scope)</c>.
    /// Consumes the code on success; counts an attempt on mismatch.
    /// </summary>
    /// <param name="subject">Consumer-defined identity key used at creation.</param>
    /// <param name="code">The code the user entered.</param>
    /// <param name="scope">Scope used at creation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OtpVerificationResult> VerifyAsync(string subject, string code, string scope, CancellationToken cancellationToken = default);
}
