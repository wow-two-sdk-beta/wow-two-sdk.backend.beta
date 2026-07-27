namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Orchestrates one-time-password creation and verification keyed by <c>(subject, scope)</c>; the subject is any consumer-defined key and the caller owns delivery (see <see cref="IOtpDeliveryHandler"/>).</summary>
public interface IOtpService
{
    /// <summary>Generates and stores a new code for <c>(subject, scope)</c>, returning it for delivery; fails with <see cref="OtpFailureReason.RateLimited"/> when one was created within the configured window.</summary>
    /// <param name="subject">Consumer-defined identity key (phone, email, username).</param>
    /// <param name="scope">Consumer-defined scope the code is valid for (e.g. <c>"crm"</c>, <c>"login"</c>).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<OtpCreationResult> CreateAsync(string subject, string scope, CancellationToken cancellationToken = default);

    /// <summary>Verifies <paramref name="code"/> against the latest pending code for <c>(subject, scope)</c>; consumes it on success, counts an attempt on mismatch.</summary>
    /// <param name="subject">Consumer-defined identity key used at creation.</param>
    /// <param name="code">The code the user entered.</param>
    /// <param name="scope">Scope used at creation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<OtpVerificationResult> VerifyAsync(string subject, string code, string scope, CancellationToken cancellationToken = default);
}
