using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Data.EntityFrameworkCore.Sqlite;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Persistence;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>Durable webhooks: protected secrets, enable and remove, one delivery id across retries, redelivery and purge.</summary>
public sealed class WebhookPersistenceTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var provider = Build(new ScriptedHandler());
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<WebhookDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task Subscriptions_ShouldPersistWithProtectedSecretsBesideSeededOnes()
    {
        await using var provider = Build(new ScriptedHandler(), seed: true);
        var subscriptions = provider.GetRequiredService<IWebhookSubscriptionRepository>();

        await subscriptions.AddAsync(Subscription("orders", "order.*", "whsec_orders"), CancellationToken.None);
        (await RawSecretAsync(provider, "orders")).Should().StartWith("dp1:").And.NotContain("whsec_orders");
        (await subscriptions.FindAsync("orders", CancellationToken.None))!.Secret.Should().Be("whsec_orders");
        (await subscriptions.ListAsync(CancellationToken.None)).Select(s => s.Id).Should().Equal("orders", "seeded");

        await subscriptions.AddAsync(Subscription("orders", "order.*", "whsec_orders") with { Enabled = false }, CancellationToken.None);
        (await subscriptions.GetMatchingAsync("order.created", CancellationToken.None)).Should().BeEmpty();
        (await subscriptions.RemoveAsync("orders", CancellationToken.None)).Should().BeTrue();
        (await subscriptions.RemoveAsync("orders", CancellationToken.None)).Should().BeFalse();

        await using var plain = Build(new ScriptedHandler(), protectSecrets: false);
        await plain.GetRequiredService<IWebhookSubscriptionRepository>().AddAsync(Subscription("raw", "*", "whsec_raw"), CancellationToken.None);
        (await RawSecretAsync(plain, "raw")).Should().Be("whsec_raw");
    }

    [Fact]
    public async Task Subscription_WhoseSecretNoLongerUnprotects_IsSkippedNotThrown()
    {
        await using (var first = Build(new ScriptedHandler()))
            await first.GetRequiredService<IWebhookSubscriptionRepository>().AddAsync(Subscription("lost", "*", "whsec_lost"), CancellationToken.None);

        await using var second = Build(new ScriptedHandler());
        (await second.GetRequiredService<IWebhookSubscriptionRepository>().ListAsync(CancellationToken.None)).Should().BeEmpty("another key ring cannot read the secret");
    }

    [Fact]
    public async Task Deliveries_ShouldKeepOneIdAcrossRetries_BeRecordedAndRedeliver()
    {
        var handler = new ScriptedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK, HttpStatusCode.OK);
        await using var provider = Build(handler);
        await provider.GetRequiredService<IWebhookSubscriptionRepository>().AddAsync(Subscription("orders", "order.*", "whsec_orders"), CancellationToken.None);
        var payload = """{"orderId":7}"""u8.ToArray();

        await provider.GetRequiredService<IWebhookPublisher>().PublishAsync("order.created", payload);

        handler.Ids.Should().HaveCount(2).And.OnlyContain(id => id == handler.Ids[0], "a retry is the same delivery");
        var deliveries = provider.GetRequiredService<IWebhookDeliveryRepository>();
        var recorded = (await deliveries.FindAsync(handler.Ids[0], CancellationToken.None))!;
        recorded.Outcome.Should().Be(WebhookDeliveryOutcome.Delivered);
        recorded.Attempts.Should().Be(2);
        recorded.Payload.ToArray().Should().Equal(payload);

        (await provider.GetRequiredService<WebhookRedeliveryService>().RedeliverAsync(handler.Ids[0])).Should().Be(WebhookDeliveryOutcome.Delivered);
        handler.Ids.Should().HaveCount(3).And.OnlyContain(id => id == handler.Ids[0], "a redelivery keeps the id so receivers drop repeats");
        handler.Bodies[^1].Should().Equal(payload);
        (await deliveries.ListAsync("orders", 10, CancellationToken.None)).Should().HaveCount(2);
        (await provider.GetRequiredService<WebhookRedeliveryService>().RedeliverAsync("unknown")).Should().BeNull();

        (await deliveries.PurgeAsync(DateTimeOffset.UtcNow.AddMinutes(1), CancellationToken.None)).Should().Be(2);
        (await deliveries.ListAsync("orders", 10, CancellationToken.None)).Should().BeEmpty();
    }

    private ServiceProvider Build(ScriptedHandler handler, bool seed = false, bool protectSecrets = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<WebhookDbContext>(o => o.UseSqlite(_connection));
        services.AddWebhookEntityFrameworkStores<WebhookDbContext>(o => o.ProtectSecrets = protectSecrets);
        services.AddWebhooks(o =>
        {
            o.BaseRetryDelay = TimeSpan.FromMilliseconds(1);
            o.MaxRetryDelay = TimeSpan.FromMilliseconds(1);
            if (seed)
                o.Subscriptions.Add(Subscription("seeded", "billing.*", "whsec_seeded"));
        });
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddHttpClient(WebhookDefaultConstants.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    private static async Task<string> RawSecretAsync(IServiceProvider provider, string id)
    {
        await using var scope = provider.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<WebhookDbContext>().Set<WebhookSubscriptionEntity>().AsNoTracking().SingleAsync(s => s.Id == id)).Secret;
    }

    private static WebhookSubscription Subscription(string id, string filter, string secret)
        => new() { Id = id, Url = new Uri("https://example.test/hooks/" + id), Secret = secret, EventTypeFilter = filter };

    /// <summary>Answers with the scripted statuses in turn, then 200, recording each delivery id and body.</summary>
    private sealed class ScriptedHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _statuses = new(statuses);

        public List<string> Ids { get; } = [];

        public List<byte[]> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Ids.Add(request.Headers.GetValues(WebhookHeaderConstants.Id).Single());
            Bodies.Add(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            return new HttpResponseMessage(_statuses.TryDequeue(out var status) ? status : HttpStatusCode.OK);
        }
    }

    private sealed class WebhookDbContext(DbContextOptions<WebhookDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyWebhookSchema();
            modelBuilder.ApplyDateTimeOffsetToBinaryConversion();
        }
    }
}
