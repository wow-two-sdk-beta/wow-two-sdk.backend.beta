namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.WebPush;

/// <summary>Holds the VAPID identity the application server signs push requests with.</summary>
public sealed record WebPushOptions
{
    /// <summary>Contact for the push service operator: <c>mailto:ops@example.com</c> or an https URL.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>VAPID public key: the uncompressed P-256 point, base64url (65 bytes); the browser subscribes with it.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>VAPID private key: the P-256 scalar, base64url (32 bytes).</summary>
    public string PrivateKey { get; set; } = string.Empty;
}
