namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Observation seam for terminal webhook-delivery outcomes. The default implementation is a no-op.</summary>
public interface IWebhookDeliveryLog
{
    /// <summary>Record a terminal delivery outcome (delivered or dropped).</summary>
    /// <param name="record">The delivery record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken);
}
