using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>Provides delivery logging into <see cref="IWebhookDeliveryRepository"/>; a failed write is logged, never thrown into the publisher.</summary>
/// <param name="deliveries">The delivery store.</param>
/// <param name="logger">Records failed writes.</param>
public sealed partial class WebhookDeliveryLoggingService(IWebhookDeliveryRepository deliveries, ILogger<WebhookDeliveryLoggingService> logger) : IWebhookDeliveryLoggingService
{
    /// <inheritdoc />
    public async ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await deliveries.AddAsync(record, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotRecorded(logger, exception, record.DeliveryId, record.SubscriptionId);
        }
    }

    [LoggerMessage(EventId = 6905, Level = LogLevel.Warning, Message = "Webhook delivery {DeliveryId} to subscription {SubscriptionId} finished but was not recorded")]
    private static partial void LogNotRecorded(ILogger logger, Exception exception, string deliveryId, string subscriptionId);
}
