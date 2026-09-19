namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>A completed (delivered or dropped) webhook delivery, handed to <see cref="IWebhookDeliveryLoggingService"/>.</summary>
public sealed record WebhookDeliveryRecord
{
    /// <summary>The target subscription id.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>The delivered event type.</summary>
    public required string EventType { get; init; }

    /// <summary>The delivery endpoint.</summary>
    public required Uri Url { get; init; }

    /// <summary>Delivered or dropped.</summary>
    public required WebhookDeliveryOutcome Outcome { get; init; }

    /// <summary>Number of HTTP attempts made.</summary>
    public required int Attempts { get; init; }

    /// <summary>Last HTTP status code received, or null when no response was received.</summary>
    public required int? StatusCode { get; init; }

    /// <summary>When the delivery reached its terminal outcome.</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }
}
