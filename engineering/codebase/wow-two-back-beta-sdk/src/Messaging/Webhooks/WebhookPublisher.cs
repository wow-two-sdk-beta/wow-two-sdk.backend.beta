using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

/// <summary>Default <see cref="IWebhookPublisher"/> — resolves matching subscriptions and fans a signed delivery out to each.</summary>
internal sealed class WebhookPublisher(IWebhookSubscriptionRepository store, HttpWebhookDispatcher dispatcher) : IWebhookPublisher
{
    public async ValueTask PublishAsync(string eventType, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);

        var subscriptions = await store.GetMatchingAsync(eventType, cancellationToken);
        foreach (var subscription in subscriptions)
            await dispatcher.DeliverAsync(subscription, eventType, payload, cancellationToken);
    }

    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        // The bytes and the Type.Name token are subscriber-visible contract — webhooks.md § Payload contract.
        var payload = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        return PublishAsync(typeof(TEvent).Name, payload, cancellationToken);
    }
}
