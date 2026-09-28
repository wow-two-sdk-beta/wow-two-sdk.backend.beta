namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Issuers;

/// <summary>
/// Defines one signature scheme's headers for an outbound delivery. Registered as an enumerable service; a subscription
/// picks one by <see cref="WebhookSubscription.SignatureScheme"/>, else <see cref="WebhookOptions.SignatureScheme"/>.
/// </summary>
public interface IWebhookSignatureIssuer
{
    /// <summary>Gets the scheme name, such as <c>wow2</c> or <c>standard</c>.</summary>
    string Scheme { get; }

    /// <summary>Issues the headers that let the receiver authenticate the delivery.</summary>
    /// <param name="signing">What the attempt signs.</param>
    IReadOnlyDictionary<string, string> Issue(WebhookSigningModel signing);
}
