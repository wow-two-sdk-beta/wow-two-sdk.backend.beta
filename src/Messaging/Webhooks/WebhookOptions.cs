namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Options for outbound webhook delivery. Uses settable properties so <c>Action&lt;WebhookOptions&gt;</c> configuration composes.</summary>
public sealed class WebhookOptions
{
    /// <summary>Subscriptions seeded into the store at startup. Add to this list inside the configure delegate.</summary>
    public IList<WebhookSubscription> Subscriptions { get; } = new List<WebhookSubscription>();

    /// <summary>Total delivery attempts per subscription before the delivery is dropped. Default 3.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base delay for the exponential-jitter retry backoff. Default 500ms.</summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>Ceiling for the retry backoff delay. Default 30s.</summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Per-attempt HTTP request timeout; a timeout is treated as a transient failure. Default 30s.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
