namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>
/// Provides redelivery of a recorded webhook with its stored body and original delivery id, so a receiver that already
/// handled it drops the repeat. The signature and timestamp are fresh.
/// </summary>
public sealed class WebhookRedeliveryService
{
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IWebhookSubscriptionRepository _subscriptions;
    private readonly HttpWebhookDispatcher _dispatcher;

    internal WebhookRedeliveryService(IWebhookDeliveryRepository deliveries, IWebhookSubscriptionRepository subscriptions, HttpWebhookDispatcher dispatcher)
    {
        _deliveries = deliveries;
        _subscriptions = subscriptions;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Sends the delivery again and returns its new outcome; null when the delivery, its stored body or an enabled
    /// subscription is missing.
    /// </summary>
    /// <param name="deliveryId">The delivery id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<WebhookDeliveryOutcome?> RedeliverAsync(string deliveryId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryId);
        var record = await _deliveries.FindAsync(deliveryId, cancellationToken);
        if (record is null || record.Payload.IsEmpty)
            return null;

        var subscription = await _subscriptions.FindAsync(record.SubscriptionId, cancellationToken);
        if (subscription is not { Enabled: true })
            return null;

        return await _dispatcher.DeliverAsync(subscription, record.EventType, record.Payload, cancellationToken, record.DeliveryId);
    }
}
