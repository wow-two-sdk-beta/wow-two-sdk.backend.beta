using System.Collections.Concurrent;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>
/// Accesses webhook subscriptions in process memory. Seeded from
/// <see cref="WebhookOptions.Subscriptions"/> at construction, thread-safe, keyed by <see cref="WebhookSubscription.Id"/>.
/// </summary>
public sealed class InMemoryWebhookSubscriptionRepository : IWebhookSubscriptionRepository
{
    private readonly ConcurrentDictionary<string, WebhookSubscription> _subscriptions = new(StringComparer.Ordinal);

    /// <summary>Create the store, seeding it from the configured <see cref="WebhookOptions.Subscriptions"/>.</summary>
    /// <param name="options">The webhook options carrying the seed subscriptions.</param>
    public InMemoryWebhookSubscriptionRepository(WebhookOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (var subscription in options.Subscriptions)
            _subscriptions[subscription.Id] = subscription;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<WebhookSubscription>> GetMatchingAsync(string eventType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        IReadOnlyList<WebhookSubscription> matches = _subscriptions.Values.Where(s => s.Enabled && s.Matches(eventType)).ToList();
        return ValueTask.FromResult(matches);
    }

    /// <inheritdoc />
    public ValueTask AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        _subscriptions[subscription.Id] = subscription;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<WebhookSubscription?> FindAsync(string id, CancellationToken cancellationToken)
        => ValueTask.FromResult(_subscriptions.GetValueOrDefault(id));

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<WebhookSubscription>> ListAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<WebhookSubscription>>([.. _subscriptions.Values]);

    /// <inheritdoc />
    public ValueTask<bool> RemoveAsync(string id, CancellationToken cancellationToken)
        => ValueTask.FromResult(_subscriptions.TryRemove(id, out _));
}
