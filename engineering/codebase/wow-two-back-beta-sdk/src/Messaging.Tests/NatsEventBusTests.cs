using AwesomeAssertions;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.Nats;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

public sealed class NatsEventBusTests : IAsyncLifetime
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
    public async Task Publishes_and_consumes_over_nats()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var host = await StartHostAsync(o => Configure(o, suffix));
        var collector = host.Services.GetRequiredService<EventCollector>();
        var bus = host.Services.GetRequiredService<IEventBus>();

        await Task.Delay(TimeSpan.FromSeconds(2)); // durable consumer provisioning + assignment
        await bus.PublishAsync(new PingEvent("over-nats"));

        (await collector.WaitForCountAsync(1, TimeSpan.FromSeconds(30))).Should().BeTrue();
        await host.StopAsync();
    }

    [Fact]
    public async Task Dead_letters_failing_message_to_dlq_subject()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var host = await StartHostAsync(
            o => Configure(o, suffix),
            retry: r => r.Retry = new RetryConfig(MaxAttempts: 2, Backoff: BackoffKind.None));
        var bus = host.Services.GetRequiredService<IEventBus>();

        await Task.Delay(TimeSpan.FromSeconds(2));
        await bus.PublishAsync(new BoomEvent("bad"));

        (await WaitForDlqMessageAsync($"s-{suffix}", $"dlq.{suffix}", TimeSpan.FromSeconds(30))).Should().BeTrue();
        await host.StopAsync();
    }

    private static void Configure(NatsOptions o, string suffix)
    {
        o.Stream = $"s-{suffix}";
        o.Subject = $"sub.{suffix}";
        o.DurableConsumer = $"g-{suffix}";
        o.DeadLetterSubject = $"dlq.{suffix}";
    }

    private async Task<IHost> StartHostAsync(Action<NatsOptions> configure, Action<InMemoryEventBusOptions>? retry = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddNatsEventBus(
            o =>
            {
                o.Url = Url;
                configure(o);
            },
            typeof(PingHandler).Assembly);
        if (retry is not null)
            builder.Services.Configure(retry);

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private async Task<bool> WaitForDlqMessageAsync(string stream, string deadLetterSubject, TimeSpan timeout)
    {
        await using var nats = new NatsConnection(new NatsOpts { Url = Url });
        var js = new NatsJSContext(nats);
        var consumer = await js.CreateOrUpdateConsumerAsync(
            stream,
            new ConsumerConfig($"dlqcheck{Guid.NewGuid():N}")
            {
                FilterSubject = deadLetterSubject,
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
            });

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await foreach (var message in consumer.ConsumeAsync<byte[]>(cancellationToken: cts.Token))
            {
                await message.AckAsync(cancellationToken: cts.Token);
                return true;
            }
        }
        catch (OperationCanceledException)
        {
            // timed out before the DLQ message arrived
        }

        return false;
    }
}
