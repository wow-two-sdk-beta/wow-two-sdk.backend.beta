namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Defines the lookup of a signature validator by scheme name.</summary>
public interface IWebhookSignatureValidatorFactory
{
    /// <summary>Creates the validator registered under <paramref name="scheme"/>.</summary>
    /// <param name="scheme">The scheme name, such as <c>stripe</c>.</param>
    /// <exception cref="InvalidOperationException">No validator is registered under the scheme.</exception>
    IWebhookSignatureValidator Create(string scheme);
}
