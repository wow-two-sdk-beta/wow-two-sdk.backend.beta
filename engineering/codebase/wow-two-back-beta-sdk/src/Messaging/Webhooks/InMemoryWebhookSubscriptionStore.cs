using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>
/// In-memory <see cref="IWebhookSubscriptionStore"/> — the zero-config default. Seeded from
/// <see cref="WebhookOptions.Subscriptions"/> at construction, thread-safe, keyed by <see cref="WebhookSubscription.Id"/>.
/// </summary>
public sealed class InMemoryWebhookSubscriptionStore : IWebhookSubscriptionStore
{
    private readonly ConcurrentDictionary<string, WebhookSubscription> _subscriptions = new(StringComparer.Ordinal);

    /// <summary>Create the store, seeding it from the configured <see cref="WebhookOptions.Subscriptions"/>.</summary>
    /// <param name="options">The webhook options carrying the seed subscriptions.</param>
    public InMemoryWebhookSubscriptionStore(IOptions<WebhookOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (var subscription in options.Value.Subscriptions)
            _subscriptions[subscription.Id] = subscription;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<WebhookSubscription>> GetMatchingAsync(string eventType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        IReadOnlyList<WebhookSubscription> matches = _subscriptions.Values.Where(s => s.Matches(eventType)).ToList();
        return ValueTask.FromResult(matches);
    }

    /// <inheritdoc />
    public ValueTask AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        _subscriptions[subscription.Id] = subscription;
        return ValueTask.CompletedTask;
    }
}
