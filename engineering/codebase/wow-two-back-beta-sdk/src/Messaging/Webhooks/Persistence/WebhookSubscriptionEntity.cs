using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

/// <summary>One stored webhook subscription; the secret is kept protected unless protection is turned off.</summary>
public class WebhookSubscriptionEntity : IKeyedEntity<string>
{
    /// <summary>The subscription id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The delivery endpoint.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The signing secret: <c>dp1:</c> and its Data Protection ciphertext, or the secret as given.</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>The event-type glob filter.</summary>
    public string EventTypeFilter { get; set; } = "*";

    /// <summary>Whether deliveries go out.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The subscriber's note.</summary>
    public string? Description { get; set; }

    /// <summary>When it was first stored.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When it was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
