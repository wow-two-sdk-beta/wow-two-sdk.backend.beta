namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Delivery-channel seam — one implementation per channel; <see cref="IOtpService.CreateAsync"/> does not deliver, the caller picks the handler and forwards the envelope.</summary>
public interface IOtpDeliveryHandler
{
    /// <summary>Sends the code to the channel-specific address in the envelope.</summary>
    /// <param name="envelope">Recipient address, code, and scope.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelope envelope, CancellationToken cancellationToken = default);
}
