using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.RabbitMq;
using WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Saga;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// <see cref="AdapterOwnedHeaderContract"/> over RabbitMQ — see that type for the bug this covers and for the
/// assertions themselves, which are shared with the Kafka, NATS and Redis Streams suites.
/// </summary>
/// <remarks>
///   - Azure Service Bus is the fifth caller of <see cref="MessageHeaderConstants.IsAdapterOwned"/>
///   - uncovered here
///   - it has no Testcontainers image, so asserting the contract against it needs a real namespace
/// </remarks>
public sealed class AdapterOwnedHeaderTests : IAsyncLifetime
{
    private static readonly AdapterOwnedHeaderContract Contract = new();
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Feature_headers_survive_the_broker_while_adapter_owned_ones_cannot_be_forged()
    {
        const string tag = "rabbitmq-header-round-trip";
        var suffix = Guid.NewGuid().ToString("N");
        using var host = await StartHostAsync(suffix);
        var harness = MessagingTestHarness.Attach(host.Services);

        var consumed = await Contract.PublishUntilConsumedAsync(harness, tag);

        Contract.AssertRoundTrip(consumed, tag);
    }

    private async Task<IHost> StartHostAsync(string suffix)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddScannedHandlerDependencies(); // PingHandler is scanned from this assembly and needs it
        builder.Services.AddRabbitMqEventBus(
            o =>
            {
                o.ConnectionString = _container.GetConnectionString();
                o.Exchange = "ex-" + suffix;
                o.Queue = "q-" + suffix;
            },
            typeof(HarnessHandler).Assembly);
        builder.Services.AddMessagingRecorder();

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
