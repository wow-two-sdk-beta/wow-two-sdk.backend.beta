namespace WoW.Two.Sdk.Backend.Beta.Identity.Otp;

/// <summary>What a delivery handler needs to send one code.</summary>
/// <param name="DeliveryAddress">Channel-specific recipient — Telegram chat id, phone number, email address.</param>
/// <param name="Code">The code to deliver.</param>
/// <param name="Scope">Scope the code was created for (drives message wording).</param>
/// <param name="Metadata">Optional channel-specific extras.</param>
public sealed record OtpDeliveryEnvelope(
    string DeliveryAddress,
    string Code,
    string Scope,
    IReadOnlyDictionary<string, string>? Metadata = null);
