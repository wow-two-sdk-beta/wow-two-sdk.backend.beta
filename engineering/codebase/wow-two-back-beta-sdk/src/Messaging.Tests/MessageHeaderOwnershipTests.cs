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
    private static readonly AdapterOwnedHeaderContract Contract = new();
    [Fact]
    public void Adapter_owned_is_strictly_narrower_than_reserved()
    {
        // The bug in one line: adapters strip the whole wt- namespace but re-stamp only the adapter-owned subset.
        MessageHeaderConstants.IsReserved(SecondLevelRetryHeaderConstants.Tier).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(SecondLevelRetryHeaderConstants.Tier).Should().BeFalse();
        MessageHeaderConstants.IsReserved(DeadLetterHeaderConstants.RedriveCount).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(DeadLetterHeaderConstants.RedriveCount).Should().BeFalse();
    }

    [Fact]
    public void Adapter_owned_is_exactly_the_set_each_adapter_re_derives_from_the_envelope()
    {
        // Pinned as a set: the predicate is only safe while it matches what the send path re-stamps.
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.EventType).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.ContentType).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.MessageId).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.PartitionKey).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.ReplyTo).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.CorrelationId).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.ConversationId).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.DeliveryCount).Should().BeTrue();

        // Death info is stamped only on a DLQ re-produce, so it is reserved-but-not-owned and rides through.
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.DeadLetterReason).Should().BeFalse();
        MessageHeaderConstants.IsAdapterOwned(MessageHeaderConstants.DeadLetterExceptionType).Should().BeFalse();

        // W3C trace context is a standard name, not an SDK-owned one — never in the reserved namespace at all.
        MessageHeaderConstants.IsReserved(MessageHeaderConstants.TraceParent).Should().BeFalse();
    }

    [Fact]
    public void Feature_headers_compose_from_the_reserved_prefix_without_changing_their_wire_value()
    {
        // These literals pin the WIRE values — a changed string here is a timeout silently dropped mid-deploy.
        SagaHeaderConstants.TimeoutName.Should().Be("wt-saga-timeout-name");
        SagaHeaderConstants.TimeoutToken.Should().Be("wt-saga-timeout-token");

        // Both reserved, so propagation blocks them, and neither adapter-owned, so both ride a re-publish.
        MessageHeaderConstants.IsReserved(SagaHeaderConstants.TimeoutName).Should().BeTrue();
        MessageHeaderConstants.IsReserved(SagaHeaderConstants.TimeoutToken).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(SagaHeaderConstants.TimeoutName).Should().BeFalse();
        MessageHeaderConstants.IsAdapterOwned(SagaHeaderConstants.TimeoutToken).Should().BeFalse();

        // The claim-check reference must survive a retry / delay / redrive hop or the rehydrator finds nothing.
        ClaimCheckHeaderConstants.Reference.Should().Be("wt-claim-check");
        MessageHeaderConstants.IsReserved(ClaimCheckHeaderConstants.Reference).Should().BeTrue();
        MessageHeaderConstants.IsAdapterOwned(ClaimCheckHeaderConstants.Reference).Should().BeFalse();
    }
}
