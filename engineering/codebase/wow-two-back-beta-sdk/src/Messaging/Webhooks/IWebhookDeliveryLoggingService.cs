namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Defines behavior that provides the terminal-outcome seam of a webhook delivery — a product records or meters each delivered or dropped delivery; the default does nothing.</summary>
public interface IWebhookDeliveryLoggingService
{
    /// <summary>Record a terminal delivery outcome (delivered or dropped).</summary>
    /// <param name="record">The delivery record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken);
}
