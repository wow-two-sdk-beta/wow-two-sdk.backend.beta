using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Testcontainers.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.InMemory;
using WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// The Redis Streams adapter against a real Redis: the round trip, competing consumers in one group, the pending-entries
/// list as an honest delivery counter, recovery of an entry stranded by a consumer that never acked, and the emulated
/// dead-letter stream.
/// </summary>
/// <remarks>
/// <para>
/// Every test provisions the consumer group itself, at <see cref="StreamPosition.Beginning"/>, before anything is
/// published. The adapter's own <c>EnsureGroups</c> then answers BUSYGROUP and keeps it. That removes the one race a
/// broker test otherwise sleeps on: the group is created at the stream's tail by the consume loop, which
/// <c>BackgroundService</c> starts without awaiting, so a publish racing startup lands ahead of the group's position and
/// is never delivered. Reading from the beginning makes publish order irrelevant instead of merely unlikely to matter.
/// </para>
/// <para>
/// The two claim-path tests strand an entry with a raw <c>XREADGROUP</c> under a consumer name the SDK never uses and
/// never acknowledge it — the crashed-instance shape the claim path exists to survive, reproduced without crashing
/// anything. Time is driven through <see cref="RedisStreamsOptions.ClaimInterval"/> and
/// <see cref="RedisStreamsOptions.MinIdleTimeBeforeClaim"/> rather than by waiting out the 5-minute default.
/// </para>
/// </remarks>
public sealed class RedisStreamsEventBusTests : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    private string Configuration => _container.GetConnectionString();

    /// <summary>Harness timings for a broker: the hop is a network round trip and a poll interval, not a channel write.</summary>
    private static MessagingHarnessOptions BrokerTimings => new()
    {
        QuietPeriod = TimeSpan.FromMilliseconds(750),
        Timeout = TimeSpan.FromSeconds(30),
    };

    private static TimeSpan Budget => TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Publishes_and_consumes_over_redis_streams()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var stream = $"wt.{suffix}";
        var group = $"g-{suffix}";

        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var database = multiplexer.GetDatabase();
        await EnsureGroupFromBeginningAsync(database, stream, group);

        using var host = await StartHostAsync(options => Configure(options, stream, group));
        var harness = MessagingTestHarness.Attach(host.Services, BrokerTimings);

        await harness.Bus.PublishAsync(new PingEvent("over-redis"));

        var consumed = await harness.Consumed.WaitForAsync<PingEvent>(timeout: Budget);
        consumed[0].BodyAs<PingEvent>().Value.Should().Be("over-redis");
        consumed[0].Outcome.Should().Be(ConsumeOutcome.Success);
        consumed[0].Destination.Should().Be(stream);

        // A read of ">" is by definition a first delivery — the contrast that makes the count of 2 in
        // Redelivered_entry_reports_the_pending_list_delivery_count a measurement rather than a constant.
        consumed[0].Envelope.DeliveryCount.Should().Be(1);

        await harness.WaitForIdleAsync();
        (await database.StreamPendingAsync(stream, group)).PendingMessageCount.Should().Be(0);

        await host.StopAsync();
    }

    [Fact]
    public async Task Competing_consumers_in_one_group_each_receive_a_different_entry()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var stream = $"wt.{suffix}";
        var group = $"g-{suffix}";

        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Configuration);
        await EnsureGroupFromBeginningAsync(multiplexer.GetDatabase(), stream, group);

        // Both handlers park on entry. With concurrency 1 the pump dispatches inline, so a parked handler holds its
        // consume loop — and BatchSize 1 caps a single read at one entry. Neither instance can therefore be holding
        // both entries when both gates report started.
        var gateA = new HarnessGate { Hold = true };
        var gateB = new HarnessGate { Hold = true };

        using var hostA = await StartHostAsync(
            options => ConfigureCompetitor(options, stream, group, $"a-{suffix}"),
            services => services.AddSingleton(gateA));
        using var hostB = await StartHostAsync(
            options => ConfigureCompetitor(options, stream, group, $"b-{suffix}"),
            services => services.AddSingleton(gateB));

        var harnessA = MessagingTestHarness.Attach(hostA.Services, BrokerTimings);
        var harnessB = MessagingTestHarness.Attach(hostB.Services, BrokerTimings);

        await harnessA.Bus.PublishAsync(new HarnessEvent("one"));
        await harnessA.Bus.PublishAsync(new HarnessEvent("two"));

        // Both entered a handler → the group handed each instance work, so consumption is genuinely shared.
        await gateA.Started.WaitAsync(Budget);
        await gateB.Started.WaitAsync(Budget);

        gateA.Release();
        gateB.Release();

        await harnessA.Consumed.WaitForAsync<HarnessEvent>(timeout: Budget);
        await harnessB.Consumed.WaitForAsync<HarnessEvent>(timeout: Budget);
        await harnessA.WaitForIdleAsync();
        await harnessB.WaitForIdleAsync();

        // The point of XREADGROUP: 2 entries over 2 members is 1 each. A broadcast adapter puts both entries in front
        // of both instances, and each of these counts reads 2.
        harnessA.Consumed.Count<HarnessEvent>().Should().Be(1);
        harnessB.Consumed.Count<HarnessEvent>().Should().Be(1);

        var delivered = harnessA.Consumed.Of<HarnessEvent>()
            .Concat(harnessB.Consumed.Of<HarnessEvent>())
            .Select(static message => message.MessageId)
            .ToArray();
        delivered.Should().HaveCount(2).And.OnlyHaveUniqueItems();

        await hostA.StopAsync();
        await hostB.StopAsync();
    }

    [Fact]
    public async Task Redelivered_entry_reports_the_pending_list_delivery_count()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var stream = $"wt.{suffix}";
        var group = $"g-{suffix}";

        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var database = multiplexer.GetDatabase();
        await EnsureGroupFromBeginningAsync(database, stream, group);

        await PublishWithoutConsumingAsync(stream, group, new PingEvent("stranded"));
        await StrandAsGhostAsync(database, stream, group);

        using var host = await StartHostAsync(options => ConfigureFastClaim(options, stream, group, $"live-{suffix}"));
        var harness = MessagingTestHarness.Attach(host.Services, BrokerTimings);

        var consumed = await harness.Consumed.WaitForAsync<PingEvent>(timeout: Budget);

        // Redis counts deliveries in the PEL, so this is the broker's own number and not a header the producer stamped:
        // 1 for the ghost's read, 2 for the claim that recovered it.
        consumed[0].Envelope.DeliveryCount.Should().Be(2);

        await host.StopAsync();
    }

    [Fact]
    public async Task Claims_an_entry_stranded_by_a_consumer_that_never_acknowledges()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var stream = $"wt.{suffix}";
        var group = $"g-{suffix}";

        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var database = multiplexer.GetDatabase();
        await EnsureGroupFromBeginningAsync(database, stream, group);

        await PublishWithoutConsumingAsync(stream, group, new PingEvent("orphan"));
        await StrandAsGhostAsync(database, stream, group);

        // Stranded: the entry is in the ghost's pending list, and XREADGROUP ">" will never hand it to anyone again.
        (await database.StreamPendingAsync(stream, group)).PendingMessageCount.Should().Be(1);

        using var host = await StartHostAsync(options => ConfigureFastClaim(options, stream, group, $"live-{suffix}"));
        var harness = MessagingTestHarness.Attach(host.Services, BrokerTimings);

        var consumed = await harness.Consumed.WaitForAsync<PingEvent>(timeout: Budget);
        consumed[0].BodyAs<PingEvent>().Value.Should().Be("orphan");
        consumed[0].Outcome.Should().Be(ConsumeOutcome.Success);

        await harness.WaitForIdleAsync();

        // Claimed, handled, acknowledged — the recovery settles the entry rather than re-claiming it every sweep.
        (await database.StreamPendingAsync(stream, group)).PendingMessageCount.Should().Be(0);

        await host.StopAsync();
    }

    [Fact]
    public async Task Dead_letters_a_failing_message_to_the_dead_letter_stream()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var stream = $"wt.{suffix}";
        var group = $"g-{suffix}";
        var deadLetterStream = $"{stream}.dlq";

        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var database = multiplexer.GetDatabase();
        await EnsureGroupFromBeginningAsync(database, stream, group);

        using var host = await StartHostAsync(
            options => Configure(options, stream, group),
            retry: reliability => reliability.Retry = new RetryConfig(MaxAttempts: 2, Backoff: BackoffKind.None));
        var harness = MessagingTestHarness.Attach(host.Services, BrokerTimings);

        await harness.Bus.PublishAsync(new BoomEvent("bad"));

        // DeadLettered is recorded after settlement, so the XADD to the dead-letter stream has already landed when this
        // returns — the assertion below reads it rather than polling for it.
        await harness.DeadLettered.WaitForAsync<BoomEvent>(timeout: Budget);
        harness.Faulted.Count<BoomEvent>().Should().Be(2); // the retry budget actually spent

        var dead = await database.StreamRangeAsync(deadLetterStream);
        dead.Should().ContainSingle();
        dead[0]["wt-dl-source-stream"].ToString().Should().Be(stream);
        dead[0]["wt-dl-reason"].HasValue.Should().BeTrue();
        dead[0]["wt-dl-exception-type"].ToString().Should().Be(typeof(InvalidOperationException).FullName);
        dead[0]["wt-body"].HasValue.Should().BeTrue(); // the original entry travels with the death fields

        // Dead-lettering settles the original: the poison entry is not left pending for the claim path to re-deliver.
        (await database.StreamPendingAsync(stream, group)).PendingMessageCount.Should().Be(0);

        await host.StopAsync();
    }

    private static void Configure(RedisStreamsOptions options, string stream, string group)
    {
        options.Stream = stream;
        options.ConsumerGroup = group;
        options.DeadLetterStream = $"{stream}.dlq";
    }

    private static void ConfigureCompetitor(RedisStreamsOptions options, string stream, string group, string consumerName)
    {
        Configure(options, stream, group);

        // The derived default is {machine}-{pid}, which is the SAME name for two buses in one test process — they would
        // share a pending-entries list. An explicit name makes these two behave as two deployed instances.
        options.ConsumerName = consumerName;

        // One entry per read, so a single instance cannot drain the stream before the other polls.
        options.BatchSize = 1;
    }

    private static void ConfigureFastClaim(RedisStreamsOptions options, string stream, string group, string consumerName)
    {
        Configure(options, stream, group);
        options.ConsumerName = consumerName;
        options.ClaimInterval = TimeSpan.FromMilliseconds(100);
        options.MinIdleTimeBeforeClaim = TimeSpan.FromMilliseconds(250);
    }

    /// <summary>Provision the group at the stream's start, so nothing published later depends on when the consume loop got around to creating it.</summary>
    private static async Task EnsureGroupFromBeginningAsync(IDatabase database, string stream, string group)
    {
        try
        {
            await database.StreamCreateConsumerGroupAsync(stream, group, StreamPosition.Beginning, createStream: true);
        }
        catch (RedisServerException exception) when (exception.Message.StartsWith("BUSYGROUP", StringComparison.Ordinal))
        {
            // Already provisioned by a previous phase of the same test.
        }
    }

    /// <summary>
    /// Put a real SDK-formatted entry on the stream with nothing consuming it — the host is built but never started, so
    /// its hosted consumer never runs while the send transport works normally.
    /// </summary>
    private async Task PublishWithoutConsumingAsync(string stream, string group, PingEvent @event)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddRedisStreamsEventBus(
            options =>
            {
                options.Configuration = Configuration;
                Configure(options, stream, group);
            },
            typeof(PingHandler).Assembly);

        using var publisher = builder.Build();
        await publisher.Services.GetRequiredService<IEventBus>().PublishAsync(@event);
    }

    /// <summary>Read the entry under a consumer name the SDK never uses and never acknowledge it — a consumer that died between delivery and ack.</summary>
    private static async Task StrandAsGhostAsync(IDatabase database, string stream, string group)
    {
        var stranded = await database.StreamReadGroupAsync(stream, group, "ghost", StreamPosition.NewMessages, count: 16);
        stranded.Should().ContainSingle();
    }

    private async Task<IHost> StartHostAsync(
        Action<RedisStreamsOptions> configure,
        Action<IServiceCollection>? configureServices = null,
        Action<InMemoryEventBusOptions>? retry = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>();
        builder.Services.AddRedisStreamsEventBus(
            options =>
            {
                options.Configuration = Configuration;
                options.PollInterval = TimeSpan.FromMilliseconds(50);
                configure(options);
            },
            typeof(PingHandler).Assembly);
        builder.Services.AddMessagingRecorder();

        if (retry is not null)
            builder.Services.Configure(retry);

        configureServices?.Invoke(builder.Services);

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
