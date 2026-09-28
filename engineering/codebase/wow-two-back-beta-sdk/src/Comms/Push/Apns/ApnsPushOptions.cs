namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.Apns;

/// <summary>Holds the APNs token-based (<c>.p8</c>) credentials and the app topic.</summary>
public sealed record ApnsPushOptions
{
    /// <summary>The Apple Developer team id (JWT issuer).</summary>
    public string TeamId { get; set; } = string.Empty;

    /// <summary>The key id of the <c>.p8</c> signing key.</summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>The <c>.p8</c> private key in PEM form (<c>-----BEGIN PRIVATE KEY-----</c>).</summary>
    public string PrivateKeyPem { get; set; } = string.Empty;

    /// <summary>The app bundle id, sent as <c>apns-topic</c>.</summary>
    public string BundleId { get; set; } = string.Empty;

    /// <summary>Use the sandbox gateway (development builds). Default false.</summary>
    public bool UseSandbox { get; set; }

    /// <summary>Gateway override for tests; null picks production or sandbox by <see cref="UseSandbox"/>.</summary>
    public Uri? BaseAddress { get; set; }
}
