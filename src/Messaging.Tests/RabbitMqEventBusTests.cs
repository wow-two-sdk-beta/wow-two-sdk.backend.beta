using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using WoW.Two.Sdk.Backend.Beta.Messaging;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

public sealed class RabbitMqEventBusTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder().Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Publishes_and_consumes_over_rabbitmq()
    {
        var suffix = Guid.NewGuid().ToString("N");
        using var host = await StartHostAsync(o => { o.Exchange = "ex-" + suffix; o.Queue = "q-" + suffix; });
        var collector = host.Services.GetRequiredService<EventCollector>();
        var bus = host.Services.GetRequiredService<IEventBus>();

        await Task.Delay(TimeSpan.FromSeconds(1)); // let topology + consumer settle
        await bus.PublishAsync(new PingEvent("over-rabbit"));

        (await collector.WaitForCountAsync(1, TimeSpan.FromSeconds(30))).Should().BeTrue();
        await host.StopAsync();
    }

    [Fact]
    public async Task Dead_letters_failing_message_to_dlq()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var dlq = "dlq-" + suffix;
        using var host = await StartHostAsync(
            o =>
            {
                o.Exchange = "ex-" + suffix;
                o.Queue = "q-" + suffix;
                o.DeadLetterExchange = "dlx-" + suffix;
                o.DeadLetterQueue = dlq;
            },
            retry: r => r.Retry = new RetryConfig(MaxAttempts: 2, Backoff: BackoffKind.None));

        var bus = host.Services.GetRequiredService<IEventBus>();

        await Task.Delay(TimeSpan.FromSeconds(1));
        await bus.PublishAsync(new BoomEvent("bad"));

        (await WaitForDlqMessageAsync(dlq, TimeSpan.FromSeconds(30))).Should().BeTrue();
        await host.StopAsync();
    }

    private async Task<IHost> StartHostAsync(Action<RabbitMqOptions> configure, Action<InMemoryEventBusOptions>? retry = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddRabbitMqEventBus(
            o =>
            {
                o.ConnectionString = _container.GetConnectionString();
                configure(o);
            },
            typeof(PingHandler).Assembly);
        if (retry is not null)
            builder.Services.Configure(retry);

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private async Task<bool> WaitForDlqMessageAsync(string deadLetterQueue, TimeSpan timeout)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_container.GetConnectionString()) };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var result = await channel.BasicGetAsync(deadLetterQueue, autoAck: true);
                if (result is not null)
                    return true;
            }
            catch (Exception)
            {
                // the queue is declared by the adapter on start — retry until it exists
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return false;
    }
}
