using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>SSRF guard: https-scheme + host-allowlist pre-flight, and connect-time blocking of private-IP targets.</summary>
public sealed class WebhookSsrfTests
{
    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class CapturingDeliveryLog : IWebhookDeliveryLog
    {
        public List<WebhookDeliveryRecord> Records { get; } = [];

        public ValueTask RecordAsync(WebhookDeliveryRecord record, CancellationToken cancellationToken)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider BuildWithStub(CountingHandler handler, Action<WebhookOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebhooks(configure);
        // Override the guarded primary handler with a stub — asserts the pre-flight blocks before any send is attempted.
        services.AddHttpClient(WebhookDefaults.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Rejects_http_target_when_https_required()
    {
        var handler = new CountingHandler();
        await using var provider = BuildWithStub(handler, o =>
        {
            o.RequireHttps = true;
            o.Subscriptions.Add(new WebhookSubscription { Url = new Uri("http://example.test/hooks"), Secret = "s", EventTypeFilter = "order.*" });
        });

        await provider.GetRequiredService<IWebhookPublisher>().PublishAsync("order.created", """{}"""u8.ToArray());

        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Does_not_deliver_to_host_outside_allowlist()
    {
        var handler = new CountingHandler();
        await using var provider = BuildWithStub(handler, o =>
        {
            o.AllowedHosts.Add("hooks.myapp.test");
            o.Subscriptions.Add(new WebhookSubscription { Url = new Uri("https://evil.test/hooks"), Secret = "s", EventTypeFilter = "order.*" });
        });

        await provider.GetRequiredService<IWebhookPublisher>().PublishAsync("order.created", """{}"""u8.ToArray());

        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Delivers_to_allowlisted_https_host()
    {
        var handler = new CountingHandler();
        await using var provider = BuildWithStub(handler, o =>
        {
            o.AllowedHosts.Add("hooks.myapp.test");
            o.Subscriptions.Add(new WebhookSubscription { Url = new Uri("https://hooks.myapp.test/x"), Secret = "s", EventTypeFilter = "order.*" });
        });

        await provider.GetRequiredService<IWebhookPublisher>().PublishAsync("order.created", """{}"""u8.ToArray());

        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Blocks_delivery_to_private_ip_target()
    {
        var log = new CapturingDeliveryLog();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebhookDeliveryLog>(log); // registered before AddWebhooks so its TryAdd defers to ours
        services.AddWebhooks(o =>
        {
            o.MaxAttempts = 3;
            o.RequestTimeout = TimeSpan.FromSeconds(2);
            // https + no allowlist → passes pre-flight; the real guarded handler must block the private IP at connect.
            o.Subscriptions.Add(new WebhookSubscription { Url = new Uri("https://127.0.0.1:9/hooks"), Secret = "s", EventTypeFilter = "order.*" });
        });
        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IWebhookPublisher>().PublishAsync("order.created", """{}"""u8.ToArray());

        log.Records.Should().ContainSingle();
        log.Records[0].Outcome.Should().Be(WebhookDeliveryOutcome.Dropped);
        log.Records[0].Attempts.Should().Be(1); // blocked at connect (Permanent) → no retries, unlike a transient connect failure
    }
}
