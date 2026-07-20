using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using Testcontainers.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// <see cref="AdapterOwnedHeaderContract"/> over Redis Streams — see that type for the bug this covers.
/// </summary>
/// <remarks>
/// <para>
/// The consumer group is provisioned at <see cref="StreamPosition.Beginning"/> before anything is published, exactly as
/// in <see cref="RedisStreamsEventBusTests"/>: the adapter's own <c>EnsureGroups</c> then answers BUSYGROUP and keeps
/// it. That removes the one race this test would otherwise sleep on — the group being created at the stream's tail by a
/// consume loop that <c>BackgroundService</c> starts without awaiting, which lets a publish racing startup land ahead of
/// the group's position and never be delivered. So one publish suffices here, where the other brokers need
/// <see cref="AdapterOwnedHeaderContract.PublishUntilConsumedAsync"/>.
/// </para>
/// <para>
/// Redis is also the one broker where the wire can be inspected cheaply after the fact (<c>XRANGE</c> is a read, not a
/// second consumer), so this suite adds an assertion the others cannot afford: exactly one <c>wt-event-type</c> field on
/// the entry. Stream fields are a list, so a caller copy the send path failed to filter would sit there beside the
/// adapter's — invisible to the decoded envelope, whose dictionary keeps only the last write.
/// </para>
/// </remarks>
public sealed class RedisStreamsAdapterOwnedHeaderTests : IAsyncLifetime
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

    [Fact]
    public async Task Feature_headers_survive_the_broker_while_adapter_owned_ones_cannot_be_forged()
    {
        const string tag = "redis-header-round-trip";
        var suffix = Guid.NewGuid().ToString("N");
        var stream = $"wt.{suffix}";
        var group = $"g-{suffix}";

        await using var multiplexer = await ConnectionMultiplexer.ConnectAsync(Configuration);
        var database = multiplexer.GetDatabase();
        await EnsureGroupFromBeginningAsync(database, stream, group);

        using var host = await StartHostAsync(stream, group);
        var harness = MessagingTestHarness.Attach(host.Services, BrokerTimings);

        await harness.Bus.PublishAsync(
            new HarnessEvent(tag),
            new PublishOptions { Headers = AdapterOwnedHeaderContract.CallerHeaders });

        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>(
            match: e => e.Tag == tag,
            timeout: TimeSpan.FromSeconds(30));

        AdapterOwnedHeaderContract.AssertRoundTrip(consumed[0], tag);

        // What the decoded envelope cannot show. The entry the adapter wrote carries the adapter's type token once, not
        // the caller's forgery plus the adapter's correction — a duplicate would decode to the right value here (last
        // field wins) while a lookup by field name elsewhere returns the first, which is the caller's.
        var entries = await database.StreamRangeAsync(stream);
        entries.Should().ContainSingle();
        CountField(entries[0], MessageHeaders.EventType).Should().Be(1);
        CountField(entries[0], SecondLevelRetryHeaders.Tier).Should().Be(1);

        await host.StopAsync();
    }

    /// <summary>How many fields of <paramref name="entry"/> carry <paramref name="name"/>.</summary>
    private static int CountField(StreamEntry entry, string name)
        => entry.Values?.Count(field => string.Equals(field.Name.ToString(), name, StringComparison.Ordinal)) ?? 0;

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

    private async Task<IHost> StartHostAsync(string stream, string group)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<EventCollector>(); // PingHandler is scanned from this assembly and needs it
        builder.Services.AddRedisStreamsEventBus(
            options =>
            {
                options.Configuration = Configuration;
                options.PollInterval = TimeSpan.FromMilliseconds(50);
                options.Stream = stream;
                options.ConsumerGroup = group;
                options.DeadLetterStream = $"{stream}.dlq";
            },
            typeof(HarnessHandler).Assembly);
        builder.Services.AddMessagingRecorder();

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
