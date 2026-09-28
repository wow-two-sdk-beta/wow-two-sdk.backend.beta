namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;

/// <summary>Refers to why an inbound webhook failed validation.</summary>
public enum WebhookSignatureFailure
{
    /// <summary>Refers to no failure: the webhook validated.</summary>
    None,

    /// <summary>Refers to a missing signature or timestamp header.</summary>
    Missing,

    /// <summary>Refers to a signature or timestamp header that does not parse.</summary>
    Malformed,

    /// <summary>Refers to a signature no configured secret produces.</summary>
    Mismatch,

    /// <summary>Refers to a signed timestamp outside the tolerance: a replay or a skewed clock.</summary>
    Stale,

    /// <summary>Refers to a body over <see cref="WebhookReceiverOptions.MaxBodyBytes"/>.</summary>
    TooLarge,
}
