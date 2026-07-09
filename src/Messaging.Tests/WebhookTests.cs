using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Outbound webhook delivery: HMAC signature + headers on a matching subscription, and no delivery on a non-match.</summary>
public sealed class WebhookTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public HttpRequestMessage? Request { get; private set; }

        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static ServiceProvider BuildProvider(CapturingHandler handler, string eventTypeFilter)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebhooks(o =>
            o.Subscriptions.Add(new WebhookSubscription
            {
                Url = new Uri("https://example.test/hooks"),
                Secret = "shh-secret",
                EventTypeFilter = eventTypeFilter,
            }));
        services.AddHttpClient(WebhookDefaults.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Delivers_signed_request_to_matching_subscription()
    {
        var handler = new CapturingHandler();
        await using var provider = BuildProvider(handler, "order.*");
        var publisher = provider.GetRequiredService<IWebhookPublisher>();
        var payload = """{"orderId":42}"""u8.ToArray();

        await publisher.PublishAsync("order.created", payload);

        handler.Calls.Should().Be(1);
        handler.Request!.Method.Should().Be(HttpMethod.Post);
        handler.Request!.RequestUri.Should().Be(new Uri("https://example.test/hooks"));
        handler.Body.Should().Equal(payload);

        handler.Request!.Headers.GetValues(WebhookHeaders.Event).Single().Should().Be("order.created");
        var timestamp = handler.Request!.Headers.GetValues(WebhookHeaders.Timestamp).Single();
        var signature = handler.Request!.Headers.GetValues(WebhookHeaders.Signature).Single();

        signature.Should().StartWith("sha256=");
        signature.Should().Be(WebhookSignature.Create("shh-secret", timestamp, payload));
    }

    [Fact]
    public async Task Does_not_deliver_to_non_matching_event_type()
    {
        var handler = new CapturingHandler();
        await using var provider = BuildProvider(handler, "billing.*");
        var publisher = provider.GetRequiredService<IWebhookPublisher>();

        await publisher.PublishAsync("order.created", """{"orderId":42}"""u8.ToArray());

        handler.Calls.Should().Be(0);
    }
}
