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
/// The ownership predicate the fix turns on, asserted directly — no broker, so it runs in microseconds and names the
/// bug shape even if the container suite is skipped.
/// </summary>
public sealed class MessageHeaderOwnershipTests
{
    [Fact]
    public void Adapter_owned_is_strictly_narrower_than_reserved()
    {
        // The bug in one line: adapters stripped on IsReserved — the whole wt- namespace — but re-stamped only the
        // adapter-owned subset. Every header in the gap was dropped at the broker with nothing to restore it. Named
        // from the features' own constants, so renaming a feature header cannot quietly desync this from reality.
        MessageHeaderConstants.IsReserved(SecondLevelRetryHeaderConstants.Tier).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(SecondLevelRetryHeaderConstants.Tier).Should().BeFalse();
        MessageHeaderConstants.IsReserved(DeadLetterHeaderConstants.RedriveCount).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(DeadLetterHeaderConstants.RedriveCount).Should().BeFalse();
    }

    [Fact]
    public void Adapter_owned_is_exactly_the_set_each_adapter_re_derives_from_the_envelope()
    {
        // Pinned as a set, because the predicate is only safe while it matches what the send path actually re-stamps:
        // add a header here without re-stamping it and it silently vanishes on every broker; drop one and a caller
        // gets to forge it.
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.EventType).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.ContentType).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.MessageId).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.PartitionKey).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.ReplyTo).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.CorrelationId).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.ConversationId).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.DeliveryCount).Should().BeTrue();

        // Dead-letter death info is stamped only by the adapter that re-produces onto a DLQ, never on a normal send,
        // so it is reserved-but-not-owned and rides through like any other feature header.
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.DeadLetterReason).Should().BeFalse();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.DeadLetterExceptionType).Should().BeFalse();

        // W3C trace context is a standard name, not an SDK-owned one — never in the reserved namespace at all.
        MessageHeaderConstants.IsReserved(MessageHeaderConstants.TraceParent).Should().BeFalse();
    }

    [Fact]
    public void Feature_headers_compose_from_the_reserved_prefix_without_changing_their_wire_value()
    {
        // Every reserved header composes from ReservedPrefix rather than spelling "wt-…" inline, so the namespace has
        // one definition and a header cannot drift out of the reserved set by typo. The saga timeout pair was the last
        // holdout. These literals pin the WIRE values: composing them was a refactor, and a message in flight during a
        // rolling deploy has to be read by both builds — a changed string here is a silently dropped timeout.
        SagaHeaderConstants.TimeoutName.Should().Be("wt-saga-timeout-name");
        SagaHeaderConstants.TimeoutToken.Should().Be("wt-saga-timeout-token");

        // And the reason composing them matters: both are reserved (so propagation blocks them) but neither is
        // adapter-owned, so both ride a re-publish instead of being stripped at the broker.
        MessageHeaderConstants.IsReserved(SagaHeaderConstants.TimeoutName).Should().BeTrue();
        MessageHeaderConstants.IsReserved(SagaHeaderConstants.TimeoutToken).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(SagaHeaderConstants.TimeoutName).Should().BeFalse();
        MessageHeaderConstants.IsAdapterOwned(SagaHeaderConstants.TimeoutToken).Should().BeFalse();

        // Same pair for the claim check, whose reference must survive a retry / delay / redrive hop or the rehydrator
        // on the far side has nothing to fetch.
        ClaimCheckHeaderConstants.Reference.Should().Be("wt-claim-check");
        MessageHeaderConstants.IsReserved(ClaimCheckHeaderConstants.Reference).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(ClaimCheckHeaderConstants.Reference).Should().BeFalse();
    }
}

/// <summary>
/// <see cref="AdapterOwnedHeaderContract"/> over RabbitMQ — see that type for the bug this covers and for the
/// assertions themselves, which are shared with the Kafka, NATS and Redis Streams suites.
/// </summary>
/// <remarks>
/// Azure Service Bus is the fifth caller of <see cref="MessageHeaderConstants.IsAdapterOwned"/> and the one broker not covered
/// here: it has no Testcontainers image, so the contract cannot be asserted against it without a real namespace.
/// </remarks>
public sealed class AdapterOwnedHeaderTests : IAsyncLifetime
{
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

        var consumed = await AdapterOwnedHeaderContract.PublishUntilConsumedAsync(harness, tag);

        AdapterOwnedHeaderContract.AssertRoundTrip(consumed, tag);
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
