namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>
/// Delivery channel seam — one implementation per channel (Telegram, SMS, email, …).
/// <see cref="IOtpService.CreateAsync"/> deliberately does NOT deliver; the caller picks the
/// handler and recipient address, then forwards the envelope.
/// </summary>
public interface IOtpDeliveryHandler
{
    /// <summary>Sends the code to the channel-specific address in the envelope.</summary>
    /// <param name="envelope">Recipient address, code, and scope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelope envelope, CancellationToken cancellationToken = default);
}
