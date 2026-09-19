namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Defines behavior that stores webhook subscriptions and resolves which ones match a given event type.</summary>
public interface IWebhookSubscriptionRepository
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
