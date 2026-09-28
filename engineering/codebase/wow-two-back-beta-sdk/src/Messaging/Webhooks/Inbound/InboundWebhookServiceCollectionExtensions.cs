using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Mediator.Idempotency;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>Registers inbound webhook reception and gates endpoints on it.</summary>
public static class InboundWebhookServiceCollectionExtensions
{
    /// <summary>
    /// Registers the receiver service, the built-in signature validators (<see cref="WebhookSchemeNameConstants"/>) and,
    /// unless one is registered, an in-memory delivery-id store. Receivers come from <paramref name="configure"/>, then
    /// the host section <c>Webhooks:Inbound</c>; every receiver needs a secret.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Receivers from code.</param>
    public static IServiceCollection AddInboundWebhooks(this IServiceCollection services, Action<InboundWebhookOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            InboundWebhookOptions.SectionName,
            configure,
            options => options.Validate(
                o => o.Receivers.Values.All(receiver => receiver.Secrets.Any(secret => !string.IsNullOrEmpty(secret))),
                "Every inbound webhook receiver needs at least one secret."));
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, Wow2WebhookSignatureValidator>(WebhookSchemeNameConstants.Wow2);
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, StandardWebhookSignatureValidator>(WebhookSchemeNameConstants.Standard);
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, StripeWebhookSignatureValidator>(WebhookSchemeNameConstants.Stripe);
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, GitHubWebhookSignatureValidator>(WebhookSchemeNameConstants.GitHub);
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, ShopifyWebhookSignatureValidator>(WebhookSchemeNameConstants.Shopify);
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, SlackWebhookSignatureValidator>(WebhookSchemeNameConstants.Slack);
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, PaddleWebhookSignatureValidator>(WebhookSchemeNameConstants.Paddle);
        services.TryAddKeyedSingleton<IWebhookSignatureValidator, TelegramWebhookSignatureValidator>(WebhookSchemeNameConstants.Telegram);
        services.TryAddSingleton<IWebhookSignatureValidatorFactory, WebhookSignatureValidatorFactory>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<WebhookReceiverService>();
        services.AddMemoryCache();
        services.TryAddSingleton<IIdempotencyRepository, InMemoryIdempotencyRepository>();
        return services;
    }

    /// <summary>
    /// Gates the endpoint on the named receiver: only a verified delivery reaches the handler, once per delivery id.
    /// Take a <see cref="WebhookReceiptModel"/> parameter and read the payload from it rather than binding the body.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint builder.</typeparam>
    /// <param name="builder">The endpoint, such as <c>app.MapPost("/webhooks/stripe", …)</c>.</param>
    /// <param name="receiver">The receiver name under <c>Webhooks:Inbound:Receivers</c>.</param>
    public static TBuilder RequireWebhookSignature<TBuilder>(this TBuilder builder, string receiver)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiver);
        return builder
            .WithMetadata(new WebhookReceiverAttribute(receiver))
            .AddEndpointFilter<TBuilder, WebhookReceiverEndpointFilter>()
            .DisableAntiforgery();
    }
}
