namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>Represents the outcome of validating an inbound webhook, with what the sender stated about the delivery.</summary>
public sealed record WebhookSignatureResult
{
    /// <summary>Gets whether the signature validated within the tolerance.</summary>
    public bool Succeeded => Failure == WebhookSignatureFailure.None;

    /// <summary>Gets why validation failed; <see cref="WebhookSignatureFailure.None"/> on success.</summary>
    public WebhookSignatureFailure Failure { get; init; }

    /// <summary>Gets the sender's delivery or event id, used to drop repeats; null when the scheme carries none.</summary>
    public string? DeliveryId { get; init; }

    /// <summary>Gets the event type the sender named, when it names one.</summary>
    public string? EventType { get; init; }

    /// <summary>Gets the signed or stated time of the delivery, when the scheme carries one.</summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Creates a failed result.</summary>
    /// <param name="failure">Why validation failed.</param>
    public static WebhookSignatureResult Failed(WebhookSignatureFailure failure) => new() { Failure = failure };
}
