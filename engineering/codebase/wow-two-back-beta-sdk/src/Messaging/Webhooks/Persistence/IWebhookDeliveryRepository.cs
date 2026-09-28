namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>Defines the store of finished webhook deliveries.</summary>
public interface IWebhookDeliveryRepository
{
    /// <summary>Stores a finished delivery.</summary>
    /// <param name="record">The delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AddAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken);

    /// <summary>The latest record of a delivery id; null when there is none.</summary>
    /// <param name="deliveryId">The delivery id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<WebhookDeliveryRecord?> FindAsync(string deliveryId, CancellationToken cancellationToken);

    /// <summary>A subscription's deliveries, newest first.</summary>
    /// <param name="subscriptionId">The subscription id.</param>
    /// <param name="limit">The most records returned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<WebhookDeliveryRecord>> ListAsync(string subscriptionId, int limit, CancellationToken cancellationToken);

    /// <summary>Deletes deliveries that finished before <paramref name="olderThan"/>, such as from a daily job.</summary>
    /// <param name="olderThan">The cut-off.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The records deleted.</returns>
    ValueTask<int> PurgeAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);
}
