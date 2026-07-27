namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Publishes an application event to matching outbound webhook subscriptions, each delivery HMAC-signed.</summary>
public interface IWebhookPublisher
{
    /// <summary>Deliver a raw payload to every subscription whose filter matches <paramref name="eventType"/>.</summary>
    /// <param name="eventType">The event type, matched against each subscription's glob filter.</param>
    /// <param name="payload">The request body — sent verbatim and signed as-is.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    /// <summary>Serialize <paramref name="event"/> to a JSON body and deliver it under the event type <c>typeof(TEvent).Name</c>.</summary>
    /// <typeparam name="TEvent">The event contract type.</typeparam>
    /// <param name="event">The event to serialize and deliver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent;
}

/// <summary>A registered webhook endpoint: where to deliver, the signing secret, and which event types it wants.</summary>
public sealed record WebhookSubscription
{
    /// <summary>Stable subscription id. Defaults to a new GUID.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Delivery endpoint — the POST target.</summary>
    public required Uri Url { get; init; }

    /// <summary>HMAC-SHA256 signing secret for this subscription.</summary>
    public required string Secret { get; init; }

    /// <summary>Event-type glob filter — <c>*</c> matches any run, <c>?</c> one char; case-insensitive. Default <c>*</c> (all).</summary>
    public string EventTypeFilter { get; init; } = "*";

    /// <summary>True when this subscription's filter matches <paramref name="eventType"/>.</summary>
    /// <param name="eventType">The event type to test.</param>
    public bool Matches(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return GlobMatch(EventTypeFilter, eventType);
    }

    // Iterative wildcard match (two-pointer with backtrack): '*' = any run, '?' = one char, case-insensitive.
    private static bool GlobMatch(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, mark = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t])))
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                mark = t;
            }
            else if (star != -1)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
            p++;
        return p == pattern.Length;
    }
}

/// <summary>Stores webhook subscriptions and resolves which ones match a given event type.</summary>
public interface IWebhookSubscriptionStore
{
    /// <summary>Return every subscription whose filter matches <paramref name="eventType"/>.</summary>
    /// <param name="eventType">The event type to match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<WebhookSubscription>> GetMatchingAsync(string eventType, CancellationToken cancellationToken);

    /// <summary>Add a subscription (replacing any existing one with the same <see cref="WebhookSubscription.Id"/>).</summary>
    /// <param name="subscription">The subscription to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken);
}

/// <summary>Terminal outcome of a webhook delivery.</summary>
public enum WebhookDeliveryOutcome
{
    /// <summary>The endpoint returned a success (2xx) response.</summary>
    Delivered,

    /// <summary>The delivery was rejected permanently, or exhausted its retry budget, and was dropped.</summary>
    Dropped,
}

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

/// <summary>Observation seam for terminal webhook-delivery outcomes. The default implementation is a no-op.</summary>
public interface IWebhookDeliveryLog
{
    /// <summary>Record a terminal delivery outcome (delivered or dropped).</summary>
    /// <param name="record">The delivery record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken);
}
