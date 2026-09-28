namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>Holds how webhook subscriptions and deliveries are stored.</summary>
/// <remarks>Set in code with <c>AddWebhookEntityFrameworkStores&lt;TContext&gt;(o => …)</c> or in the host section <c>Webhooks:Persistence</c>, applied last.</remarks>
public sealed record WebhookPersistenceOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Webhooks:Persistence";

    /// <summary>Gets or sets whether secrets are stored through ASP.NET Data Protection; the key ring must persist. Default true.</summary>
    public bool ProtectSecrets { get; set; } = true;

    /// <summary>Gets or sets whether delivery bodies are kept, which redelivery needs. Default true.</summary>
    public bool StorePayloads { get; set; } = true;

    /// <summary>Gets or sets the largest body kept, in bytes; larger ones are logged without it. Default 256 KiB.</summary>
    public int MaxStoredPayloadBytes { get; set; } = 256 * 1024;
}
