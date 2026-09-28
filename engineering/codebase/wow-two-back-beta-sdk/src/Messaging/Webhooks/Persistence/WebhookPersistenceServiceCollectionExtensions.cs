using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>Registers durable webhook subscriptions and deliveries over Entity Framework Core.</summary>
public static class WebhookPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Stores subscriptions and finished deliveries in <typeparamref name="TContext"/> (map them with
    /// <c>ApplyWebhookSchema()</c>), replacing the in-memory store and the no-op log, and registers
    /// <see cref="WebhookRedeliveryService"/>. Use with <c>AddWebhooks</c>, in either order. Options come from
    /// <paramref name="configure"/>, then the host section <c>Webhooks:Persistence</c>.
    /// </summary>
    /// <typeparam name="TContext">The context hosting the webhook schema, registered with <c>AddDbContext</c>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Secret protection and payload keeping.</param>
    public static IServiceCollection AddWebhookEntityFrameworkStores<TContext>(this IServiceCollection services, Action<WebhookPersistenceOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddModuleOptions(
            WebhookPersistenceOptions.SectionName,
            configure,
            builder => builder.Validate(o => o.MaxStoredPayloadBytes >= 0, "WebhookPersistenceOptions.MaxStoredPayloadBytes must not be negative."));
        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.Replace(ServiceDescriptor.Singleton<IWebhookSubscriptionRepository, EfWebhookSubscriptionRepository<TContext>>());
        services.Replace(ServiceDescriptor.Singleton<IWebhookDeliveryRepository, EfWebhookDeliveryRepository<TContext>>());
        services.Replace(ServiceDescriptor.Singleton<IWebhookDeliveryLoggingService, WebhookDeliveryLoggingService>());
        services.TryAddSingleton(serviceProvider => new WebhookRedeliveryService(
            serviceProvider.GetRequiredService<IWebhookDeliveryRepository>(),
            serviceProvider.GetRequiredService<IWebhookSubscriptionRepository>(),
            serviceProvider.GetRequiredService<HttpWebhookDispatcher>()));
        return services;
    }
}
