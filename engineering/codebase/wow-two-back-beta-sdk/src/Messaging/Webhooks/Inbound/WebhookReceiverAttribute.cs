namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>Marks an endpoint as the named inbound webhook receiver; <c>RequireWebhookSignature</c> adds it as metadata.</summary>
/// <param name="receiver">The receiver name under <c>Webhooks:Inbound:Receivers</c>.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public sealed class WebhookReceiverAttribute(string receiver) : Attribute
{
    /// <summary>Gets the receiver name.</summary>
    public string Receiver { get; } = receiver;
}
