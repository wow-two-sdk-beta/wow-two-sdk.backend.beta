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

        // Deliberately NOT routed through IMessageSerializer / IMessageTypeMapper, unlike the transports and the outbox:
        // both seams change bytes that leave the process and that subscribers already depend on.
        //   - Body: the seam's default serializer uses JsonOptionsConstants (camelCase, null-omitting, relaxed escaping,
        //     NodaTime converters); this call uses JsonSerializerOptions.Default (PascalCase, nulls written, strict
        //     escaping). Different bytes mean a different signed body (WebhookSignatureHasher signs the payload verbatim) and
        //     a renamed JSON property on every field, breaking every existing subscriber's parser.
        //   - Event type: the resolver emits a FullName token ("MyApp.Events.OrderPlaced"); this sends Type.Name
        //     ("OrderPlaced"), which is what subscribers' EventTypeFilter globs and X-Webhook-Event routing match on.
        // Either swap is a subscriber-visible contract break, so it needs an opt-in switch or a v2 signature scheme.
        // Callers wanting another format serialize themselves and use the raw-payload overload.
        var payload = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        return PublishAsync(typeof(TEvent).Name, payload, cancellationToken);
    }
}
