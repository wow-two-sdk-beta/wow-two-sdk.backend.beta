using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Backend.Beta.Messaging.Nats;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// <see cref="AdapterOwnedHeaderContract"/> over NATS JetStream — see that type for the bug this covers.
/// </summary>
/// <remarks>
/// JetStream carries headers in a <c>NatsHeaders</c> dictionary, so an unfiltered caller header cannot ride the wire
/// beside the adapter's own copy the way it can on Kafka — it can only shadow it, and only if the adapter stops
/// writing its own value afterwards.
/// </remarks>
public sealed class NatsAdapterOwnedHeaderTests : IAsyncLifetime
{
    // Generic Testcontainers (no NATS module) — nats:2.10 with `-js` enables JetStream.
    // The nats image is shell-less (distroless), so an exec-based port probe can't run; wait on the
    // server's ready log line instead (read via the Docker API, no in-container shell needed).
    private readonly IContainer _container = new ContainerBuilder()
        .WithImage("nats:2.10")
        .WithCommand("-js")
        .WithPortBinding(4222, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server is ready"))
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private string Url => $"nats://{_container.Hostname}:{_container.GetMappedPublicPort(4222)}";

    [Fact]
    public async Task Feature_headers_survive_the_broker_while_adapter_owned_ones_cannot_be_forged()
    {
        const string tag = "nats-header-round-trip";
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
        builder.Services.AddNatsEventBus(
            o =>
            {
                o.Url = Url;
                o.Stream = $"s-{suffix}";
                o.Subject = $"sub.{suffix}";
                o.DurableConsumer = $"g-{suffix}";
                o.DeadLetterSubject = $"dlq.{suffix}";
            },
            typeof(HarnessHandler).Assembly);
        builder.Services.AddMessagingRecorder();

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
