using System.Net;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Webhooks;

/// <summary>Webhook endpoints gated by <c>RequireWebhookSignature</c> through an <c>AddApiDefaults</c> host configured by the host.</summary>
public sealed class InboundWebhookEndpointTests
{
    private const string Secret = "whsec_endpoint_secret";

    [Fact]
    public async Task VerifiedDelivery_ReachesTheHandlerOnce()
    {
        await using var host = await StartAsync();

        using var first = await host.SendAsync(Event("evt_1"));
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        host.Handled.Should().Equal("evt_1:invoice.paid");

        using var repeat = await host.SendAsync(Event("evt_1"));
        repeat.StatusCode.Should().Be(HttpStatusCode.Accepted, "a handled delivery answers with its first status");
        host.Handled.Should().HaveCount(1);

        (await host.SendAsync(Event("evt_2"))).StatusCode.Should().Be(HttpStatusCode.Accepted);
        host.Handled.Should().HaveCount(2);
    }

    [Fact]
    public async Task ForgedStaleOrOversizedDeliveries_NeverReachTheHandler()
    {
        await using var host = await StartAsync();
        var body = Event("evt_forged");

        using var forged = await host.SendAsync(body, secret: "whsec_attacker");
        forged.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await forged.Content.ReadAsStringAsync()).Should().Contain("The webhook signature is not valid.");

        (await host.SendAsync(body, signedAt: DateTimeOffset.UtcNow.AddMinutes(-10))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.SendAsync(body, signature: string.Empty)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await host.SendAsync(Encoding.UTF8.GetString(new byte[3000]).Replace('\0', 'x'))).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        host.Handled.Should().BeEmpty();
    }

    [Fact]
    public async Task FailedHandling_StaysRetryable()
    {
        await using var host = await StartAsync();
        host.FailNext = true;

        (await host.SendAsync(Event("evt_retry"))).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await host.SendAsync(Event("evt_retry"))).StatusCode.Should().Be(HttpStatusCode.Accepted);
        host.Handled.Should().Equal("evt_retry:invoice.paid", "evt_retry:invoice.paid");
    }

    private static string Event(string id) => $"{{\"id\":\"{id}\",\"object\":\"event\",\"type\":\"invoice.paid\"}}";

    private static async Task<WebhookHost> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Webhooks:Inbound:Receivers:billing:Scheme"] = "stripe",
            ["Webhooks:Inbound:Receivers:billing:Secrets:0"] = Secret,
            ["Webhooks:Inbound:Receivers:billing:MaxBodyBytes"] = "2048",
        });
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
        });
        builder.Services.AddInboundWebhooks();
        var app = builder.Build();
        app.UseApiDefaults();
        var host = new WebhookHost(app);
        app.MapPost("/webhooks/billing", (WebhookReceiptModel webhook) =>
        {
            var payload = webhook.ReadJson<StripeEvent>()!;
            host.Handled.Add($"{payload.Id}:{webhook.EventType}");
            if (host.FailNext)
            {
                host.FailNext = false;
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Accepted();
        }).RequireWebhookSignature("billing");
        await app.StartAsync();
        return host;
    }

    private sealed record StripeEvent
    {
        public required string Id { get; init; }
    }

    private sealed class WebhookHost(WebApplication app) : IAsyncDisposable
    {
        public List<string> Handled { get; } = [];

        public bool FailNext { get; set; }

        public async Task<HttpResponseMessage> SendAsync(string body, string secret = Secret, DateTimeOffset? signedAt = null, string? signature = null)
        {
            var timestamp = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
            var hex = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));
            using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/billing") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.TryAddWithoutValidation("Stripe-Signature", signature ?? $"t={timestamp},v1={hex}");
            return await app.GetTestServer().CreateClient().SendAsync(request);
        }

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
