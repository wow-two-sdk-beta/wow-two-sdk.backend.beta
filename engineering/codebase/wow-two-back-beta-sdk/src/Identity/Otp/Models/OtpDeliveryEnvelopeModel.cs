namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

/// <summary>Represents the code, recipient and context for an OTP delivery.</summary>
public sealed record OtpDeliveryEnvelopeModel
{
    /// <summary>Channel-specific recipient — Telegram chat id, phone number, email address.</summary>
    public required string DeliveryAddress { get; init; }

    /// <summary>The code to deliver.</summary>
    public required string Code { get; init; }

    /// <summary>Scope the code was created for (drives message wording).</summary>
    public required string Scope { get; init; }

    /// <summary>Optional channel-specific extras.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
