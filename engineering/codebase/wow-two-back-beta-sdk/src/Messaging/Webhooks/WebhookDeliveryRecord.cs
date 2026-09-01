namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>A completed (delivered or dropped) webhook delivery, handed to <see cref="IWebhookDeliveryLog"/>.</summary>
/// <param name="SubscriptionId">The target subscription id.</param>
/// <param name="EventType">The delivered event type.</param>
/// <param name="Url">The delivery endpoint.</param>
/// <param name="Outcome">Delivered or dropped.</param>
/// <param name="Attempts">Number of HTTP attempts made.</param>
/// <param name="StatusCode">Last HTTP status code received, or null when no response was received.</param>
/// <param name="OccurredAtUtc">When the delivery reached its terminal outcome.</param>
public sealed record WebhookDeliveryRecord(
    string SubscriptionId,
    string EventType,
    Uri Url,
    WebhookDeliveryOutcome Outcome,
    int Attempts,
    int? StatusCode,
    DateTimeOffset OccurredAtUtc);
