using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>DI registration for outbound webhook delivery.</summary>
public static class WebhooksServiceCollectionExtensions
{
    /// <summary>
    /// Register outbound webhooks: the <see cref="IWebhookPublisher"/>, the in-memory subscription store, a no-op
    /// delivery log, the retry policy, and the named delivery <c>HttpClient</c>. Idempotent — safe to call repeatedly,
    /// and any registered service can be replaced by registering your own before or after this call.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options — subscriptions seed, retry budget, timeout.</param>
    public static IServiceCollection AddWebhooks(this IServiceCollection services, Action<WebhookOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<WebhookOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);
        optionsBuilder
            .Validate(static o => o.MaxAttempts >= 1, "Webhooks: MaxAttempts must be >= 1.")
            .Validate(static o => o.Subscriptions.All(s => !string.IsNullOrEmpty(s.Secret)), "Webhooks: every subscription must have a secret.")
            .ValidateOnStart();

        services.AddHttpClient(WebhookDefaults.HttpClientName);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRetryPolicy, DefaultRetryPolicy>();
        services.TryAddSingleton<IWebhookDeliveryLog, NoopWebhookDeliveryLog>();
        services.TryAddSingleton<IWebhookSubscriptionStore, InMemoryWebhookSubscriptionStore>();
        services.TryAddSingleton<HttpWebhookDispatcher>();
        services.TryAddSingleton<IWebhookPublisher, WebhookPublisher>();
        return services;
    }
}
