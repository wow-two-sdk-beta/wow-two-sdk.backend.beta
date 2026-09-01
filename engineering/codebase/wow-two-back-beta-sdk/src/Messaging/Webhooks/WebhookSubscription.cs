namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

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
