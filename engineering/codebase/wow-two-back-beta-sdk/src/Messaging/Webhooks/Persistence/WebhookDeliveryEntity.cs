using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>One finished delivery — delivered or dropped — with the body when payloads are kept.</summary>
public class WebhookDeliveryEntity : IKeyedEntity<Guid>
{
    /// <summary>The row id; a redelivery adds a row under the same <see cref="DeliveryId"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>The delivery id sent as <c>X-Webhook-Id</c>.</summary>
    public string DeliveryId { get; set; } = string.Empty;

    /// <summary>The target subscription.</summary>
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>The event type.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The endpoint it went to.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Delivered or dropped.</summary>
    public WebhookDeliveryOutcome Outcome { get; set; }

    /// <summary>The HTTP attempts made.</summary>
    public int Attempts { get; set; }

    /// <summary>The last HTTP status received.</summary>
    public int? StatusCode { get; set; }

    /// <summary>When it finished.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The body sent; null when payloads are not kept or it was over the size cap.</summary>
    public byte[]? Payload { get; set; }
}
