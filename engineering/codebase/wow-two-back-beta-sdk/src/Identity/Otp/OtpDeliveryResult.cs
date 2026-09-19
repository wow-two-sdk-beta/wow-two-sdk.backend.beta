namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>Represents outcome of a delivery attempt.</summary>
public sealed record OtpDeliveryResult
{
    /// <summary>Whether the channel accepted the message.</summary>
    public required bool Success { get; init; }

    /// <summary>Channel-specific failure detail (present on failure).</summary>
    public string? FailureReason { get; init; }
}
