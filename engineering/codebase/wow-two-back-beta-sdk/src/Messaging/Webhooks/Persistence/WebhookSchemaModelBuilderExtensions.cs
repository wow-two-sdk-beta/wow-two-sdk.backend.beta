using Microsoft.EntityFrameworkCore;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>Maps the webhook tables onto a product's context.</summary>
public static class WebhookSchemaModelBuilderExtensions
{
    /// <summary>Maps <c>webhook_subscriptions</c> and <c>webhook_deliveries</c>; call from <c>OnModelCreating</c>.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    public static ModelBuilder ApplyWebhookSchema(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<WebhookSubscriptionEntity>(subscription =>
        {
            subscription.ToTable("webhook_subscriptions");
            subscription.HasKey(s => s.Id);
            subscription.Property(s => s.Id).HasMaxLength(64);
            subscription.Property(s => s.Url).HasMaxLength(2048);
            subscription.Property(s => s.Secret).HasMaxLength(2048);
            subscription.Property(s => s.EventTypeFilter).HasMaxLength(200);
            subscription.Property(s => s.Description).HasMaxLength(500);
        });
        modelBuilder.Entity<WebhookDeliveryEntity>(delivery =>
        {
            delivery.ToTable("webhook_deliveries");
            delivery.HasKey(d => d.Id);
            delivery.HasIndex(d => d.DeliveryId);
            delivery.HasIndex(d => new { d.SubscriptionId, d.OccurredAt });
            delivery.Property(d => d.DeliveryId).HasMaxLength(64);
            delivery.Property(d => d.SubscriptionId).HasMaxLength(64);
            delivery.Property(d => d.EventType).HasMaxLength(200);
            delivery.Property(d => d.Url).HasMaxLength(2048);
            delivery.Property(d => d.Outcome).HasConversion<string>().HasMaxLength(20);
        });
        return modelBuilder;
    }
}
