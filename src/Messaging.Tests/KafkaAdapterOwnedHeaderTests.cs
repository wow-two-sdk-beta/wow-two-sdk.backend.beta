using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Kafka;
using WoW.Two.Sdk.Backend.Beta.Messaging.Kafka;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// <see cref="AdapterOwnedHeaderContract"/> over Kafka — see that type for the bug this covers.
/// </summary>
/// <remarks>
/// Kafka is the transport where the strip predicate matters most literally: its <c>Headers</c> collection permits
/// duplicate keys, so a caller header that the send path fails to filter rides the wire <em>alongside</em> the
/// adapter's own copy rather than replacing it, and both reach the consumer's decode.
/// </remarks>
public sealed class KafkaAdapterOwnedHeaderTests : IAsyncLifetime
{
    private readonly KafkaContainer _container = new KafkaBuilder().WithImage("confluentinc/cp-kafka:7.5.0").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Feature_headers_survive_the_broker_while_adapter_owned_ones_cannot_be_forged()
    {
        const string tag = "kafka-header-round-trip";
        var suffix = Guid.NewGuid().ToString("N");
        using var host = await StartHostAsync(suffix);
        var harness = MessagingTestHarness.Attach(host.Services);

        var consumed = await AdapterOwnedHeaderContract.PublishUntilConsumedAsync(harness, tag);

        AdapterOwnedHeaderContract.AssertRoundTrip(consumed, tag);

        await host.StopAsync();
    }

    private async Task<IHost> StartHostAsync(string suffix)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>(); // PingHandler is scanned from this assembly and needs it
        builder.Services.AddKafkaEventBus(
            o =>
            {
                o.BootstrapServers = _container.GetBootstrapAddress();
                o.Topic = "t-" + suffix;
                o.GroupId = "g-" + suffix;
                o.DeadLetterTopic = "dlq-" + suffix;
            },
            typeof(HarnessHandler).Assembly);
        builder.Services.AddMessagingRecorder();

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
