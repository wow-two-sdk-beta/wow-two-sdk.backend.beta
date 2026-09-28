namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Defines behavior that stores webhook subscriptions and resolves which ones match a given event type.</summary>
public interface IWebhookSubscriptionRepository
{
    /// <summary>Return every enabled subscription whose filter matches <paramref name="eventType"/>.</summary>
    /// <param name="eventType">The event type to match.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<WebhookSubscription>> GetMatchingAsync(string eventType, CancellationToken cancellationToken);

    /// <summary>Add a subscription (replacing any existing one with the same <see cref="WebhookSubscription.Id"/>).</summary>
    /// <param name="subscription">The subscription to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken);

    /// <summary>The subscription with <paramref name="id"/>, enabled or not; null when there is none.</summary>
    /// <param name="id">The subscription id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<WebhookSubscription?> FindAsync(string id, CancellationToken cancellationToken);

    /// <summary>Every subscription, enabled or not.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<IReadOnlyList<WebhookSubscription>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Removes the subscription; false when there is none.</summary>
    /// <param name="id">The subscription id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<bool> RemoveAsync(string id, CancellationToken cancellationToken);
}
