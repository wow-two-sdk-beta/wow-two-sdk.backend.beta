using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

/// <summary>Creates signature validators from the keyed registrations, built-in and the product's own.</summary>
/// <param name="services">The provider holding the keyed validators.</param>
public sealed class WebhookSignatureValidatorFactory(IServiceProvider services) : IWebhookSignatureValidatorFactory
{
    /// <inheritdoc />
    public IWebhookSignatureValidator Create(string scheme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        return services.GetKeyedService<IWebhookSignatureValidator>(scheme.ToLowerInvariant())
            ?? throw new InvalidOperationException($"No webhook signature validator is registered for the scheme '{scheme}'.");
    }
}
