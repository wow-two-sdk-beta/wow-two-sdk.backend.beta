using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Http.Safety;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Issuers;

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

        services.AddValidatedOptions(
            configure,
            static builder => builder
                .Validate(static o => o.MaxAttempts >= 1, "Webhooks: MaxAttempts must be >= 1.")
                .Validate(static o => o.BaseRetryDelay >= TimeSpan.Zero, "Webhooks: BaseRetryDelay must not be negative.")
                .Validate(static o => o.MaxRetryDelay >= o.BaseRetryDelay, "Webhooks: MaxRetryDelay must be at least BaseRetryDelay.")
                .Validate(static o => o.RequestTimeout > TimeSpan.Zero, "Webhooks: RequestTimeout must be positive.")
                .Validate(static o => o.Subscriptions.All(s => !string.IsNullOrEmpty(s.Secret)), "Webhooks: every subscription must have a secret."));

        // Block unsafe resolved target addresses at connect time through the shared outbound policy.
        services.AddHttpClient(WebhookDefaultConstants.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static sp =>
            {
                var webhookOptions = sp.GetRequiredService<WebhookOptions>();
                var handler = new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    UseProxy = false,
                    UseCookies = false,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
                };
                if (!webhookOptions.AllowPrivateNetworkTargets)
                    handler.ConnectCallback = SafeOutboundHttpClientBuilderExtensions.ConnectAsync;
                return handler;
            });

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRetryPolicy, RetryPolicy>();
        services.TryAddSingleton<IWebhookDeliveryLoggingService, NoopWebhookDeliveryLoggingService>();
        services.TryAddSingleton<IWebhookSubscriptionRepository, InMemoryWebhookSubscriptionRepository>();
        // The wow2 scheme's hasher stays swappable; other schemes are issuers a subscription names.
        services.TryAddSingleton<IWebhookSignatureHasher, WebhookSignatureHasher>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebhookSignatureIssuer, Wow2WebhookSignatureIssuer>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IWebhookSignatureIssuer, StandardWebhookSignatureIssuer>());
        services.TryAddSingleton<HttpWebhookDispatcher>();
        services.TryAddSingleton<IWebhookPublisher, WebhookPublisher>();
        return services;
    }
}
