using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Issuers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Outbound signature schemes: the Standard Webhooks example, a round trip into the inbound validator, and unknown schemes.</summary>
public sealed class WebhookSignatureIssuerTests
{
    private const string StandardSecret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw";

    [Fact]
    public void StandardIssuer_ShouldReproduceThePublishedExample()
    {
        var headers = new StandardWebhookSignatureIssuer().Issue(new WebhookSigningModel
        {
            Secret = StandardSecret,
            DeliveryId = "msg_p5jXN8AQM9LWM0D4loKWxJek",
            EventType = "test",
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(1614265330),
            Payload = Encoding.UTF8.GetBytes("{\"test\": 2432232314}"),
        });

        headers["webhook-signature"].Should().Be("v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=");
        headers["webhook-timestamp"].Should().Be("1614265330");
    }

    [Fact]
    public async Task StandardSubscription_ShouldBeAcceptedByTheInboundValidator()
    {
        var handler = new CapturingHandler();
        await using var provider = Build(handler, new WebhookSubscription
        {
            Url = new Uri("https://example.test/hooks"),
            Secret = StandardSecret,
            SignatureScheme = WebhookSchemeNameConstants.Standard,
        });
        var payload = """{"type":"invoice.paid","data":{}}"""u8.ToArray();

        await provider.GetRequiredService<IWebhookPublisher>().PublishAsync("invoice.paid", payload);

        var request = handler.Requests.Single();
        request.Should().ContainKeys("webhook-id", "webhook-timestamp", "webhook-signature").And.NotContainKey(WebhookHeaderConstants.Signature);
        var receiver = new WebhookReceiverOptions();
        receiver.Secrets.Add(StandardSecret);
        var result = new StandardWebhookSignatureValidator().Validate(new HeaderDictionary(request.ToDictionary(pair => pair.Key, pair => new Microsoft.Extensions.Primitives.StringValues(pair.Value))), payload, receiver, DateTimeOffset.UtcNow);
        result.Succeeded.Should().BeTrue();
        result.EventType.Should().Be("invoice.paid");
    }

    [Fact]
    public async Task UnknownScheme_ShouldDropWithoutSending()
    {
        var handler = new CapturingHandler();
        await using var provider = Build(handler, new WebhookSubscription { Url = new Uri("https://example.test/hooks"), Secret = "s", SignatureScheme = "acme" });

        await provider.GetRequiredService<IWebhookPublisher>().PublishAsync("order.created", "{}"u8.ToArray());

        handler.Requests.Should().BeEmpty();
    }

    private static ServiceProvider Build(CapturingHandler handler, WebhookSubscription subscription)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebhooks(o => o.Subscriptions.Add(subscription));
        services.AddHttpClient(WebhookDefaultConstants.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    /// <summary>Records each request's headers and answers 200.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<Dictionary<string, string>> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
