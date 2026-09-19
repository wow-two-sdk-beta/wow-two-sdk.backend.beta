namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Defines behavior that publishes an application event to matching outbound webhook subscriptions, each delivery HMAC-signed.</summary>
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
