namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>Holds how one receiver validates its sender: the signature scheme, the secrets and the replay guards.</summary>
public sealed record WebhookReceiverOptions
{
    /// <summary>Gets or sets the signature scheme (<see cref="WebhookSchemeNameConstants"/>); null uses the receiver's name.</summary>
    public string? Scheme { get; set; }

    /// <summary>Gets the signing secrets; any one validates, so a rotation lists the new secret beside the old.</summary>
    public List<string> Secrets { get; } = [];

    /// <summary>Gets or sets how far a signed timestamp may drift from now. Default 5 minutes; zero turns the check off.</summary>
    public TimeSpan Tolerance { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Gets or sets whether a delivery id already handled answers without running the handler again. Default true.</summary>
    public bool Deduplicate { get; set; } = true;

    /// <summary>Gets or sets how long a handled delivery id is remembered. Default 3 days, past typical provider retries.</summary>
    public TimeSpan DeduplicationWindow { get; set; } = TimeSpan.FromDays(3);

    /// <summary>Gets or sets the largest body read for validation, in bytes. Default 1 MiB.</summary>
    public int MaxBodyBytes { get; set; } = 1024 * 1024;
}
