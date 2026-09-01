namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Outcome of a delivery attempt.</summary>
/// <param name="Success">Whether the channel accepted the message.</param>
/// <param name="FailureReason">Channel-specific failure detail (present on failure).</param>
public sealed record OtpDeliveryResult(bool Success, string? FailureReason = null);
