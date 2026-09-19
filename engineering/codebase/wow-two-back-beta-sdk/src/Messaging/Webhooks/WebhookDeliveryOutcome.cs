namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Refers to terminal outcome of a webhook delivery.</summary>
public enum WebhookDeliveryOutcome
{
    /// <summary>The endpoint returned a success (2xx) response.</summary>
    Delivered,

    /// <summary>The delivery was rejected permanently, or exhausted its retry budget, and was dropped.</summary>
    Dropped,
}
