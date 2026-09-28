namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>Holds the inbound webhook receivers, keyed by the name endpoints require.</summary>
/// <remarks>Set in code with <c>AddInboundWebhooks(o => …)</c> or in the host section <c>Webhooks:Inbound</c>, which is applied last.</remarks>
public sealed record InboundWebhookOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Webhooks:Inbound";

    /// <summary>Gets the receivers by name, such as <c>stripe</c> or <c>github</c>; names ignore case.</summary>
    public Dictionary<string, WebhookReceiverOptions> Receivers { get; } = new(StringComparer.OrdinalIgnoreCase);
}
