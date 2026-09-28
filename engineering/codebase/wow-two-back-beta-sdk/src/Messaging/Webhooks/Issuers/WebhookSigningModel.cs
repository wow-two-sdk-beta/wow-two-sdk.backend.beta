namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Issuers;

/// <summary>Represents what one delivery attempt signs.</summary>
public sealed record WebhookSigningModel
{
    /// <summary>Gets the subscription's secret.</summary>
    public required string Secret { get; init; }

    /// <summary>Gets the delivery id, the same on every retry.</summary>
    public required string DeliveryId { get; init; }

    /// <summary>Gets the event type.</summary>
    public required string EventType { get; init; }

    /// <summary>Gets the signing time.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the body, signed as sent.</summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }
}
