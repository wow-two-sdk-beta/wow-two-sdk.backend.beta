using System.Diagnostics;
using AwesomeAssertions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// The <c>wt-</c> header contract, defined once and asserted identically against every containerisable broker:
/// RabbitMQ, Kafka, NATS JetStream and Redis Streams.
/// </summary>
/// <remarks>
///   - a reserved <c>wt-</c> header no adapter owns survives the round trip
///   - an adapter-owned header is re-stamped
///   - needs a real broker — the in-memory transport never serializes headers
/// </remarks>
internal sealed class AdapterOwnedHeaderContract
{
    /// <summary>Holds a type token nothing can resolve — what a caller forges onto an adapter-owned key.</summary>
    public const string ForgedEventType = "forged.contract.DoesNotExist";

    /// <summary>How long a broker gets to bring its subscription up before <see cref="PublishUntilConsumedAsync"/> gives up.</summary>
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(60);

    /// <summary>One publish attempt's share of that budget.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// What the caller hands the bus: two reserved-but-not-adapter-owned feature headers, an unreserved control, and a
    /// forgery attempt on an adapter-owned key.
    /// </summary>
    public IReadOnlyDictionary<string, string> CallerHeaders { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SecondLevelRetryHeaderConstants.Tier] = "2",
        [DeadLetterHeaderConstants.RedriveCount] = "3",
        ["tenant-id"] = "acme", // unreserved: the easy case, and the control for the two above
        [MessageHeaderConstants.EventType] = ForgedEventType, // adapter-owned — a forgery attempt
    };

    /// <summary>Assert both halves of the contract on a message that made the round trip through a real broker.</summary>
    /// <param name="consumed">The recorded consumption.</param>
    /// <param name="tag">The tag the message was published with.</param>
    public void AssertRoundTrip(RecordedMessage consumed, string tag)
    {
        var received = consumed.Envelope.Headers;

        // Reserved but not adapter-owned: dropping these is data loss, and the feature reading them never fires.
        received.Should().ContainKey(SecondLevelRetryHeaderConstants.Tier).WhoseValue.Should().Be("2");
        received.Should().ContainKey(DeadLetterHeaderConstants.RedriveCount).WhoseValue.Should().Be("3");
        received.Should().ContainKey("tenant-id").WhoseValue.Should().Be("acme");

        // Read back through each feature's own reader — surviving as text is only half the promise.
        SecondLevelRetryHeaderConstants.ReadTier(consumed.Envelope).Should().Be(2);
        DeadLetterHeaderConstants.ReadRedriveCount(consumed.Envelope).Should().Be(3);

        // The other half: the adapter overwrites the caller's forged type token with the real one.
        received.Should().ContainKey(MessageHeaderConstants.EventType).WhoseValue.Should().Be(typeof(HarnessEvent).FullName);
        received[MessageHeaderConstants.EventType].Should().NotBe(ForgedEventType);

        // Consumption is itself the proof: a forgery that stuck would fail type resolution and record nothing.
        consumed.BodyAs<HarnessEvent>().Tag.Should().Be(tag);
        received.Should().ContainKey(MessageHeaderConstants.ContentType).WhoseValue.Should().Be("application/json");
    }

    /// <summary>Publish until the consumer records it, then return that record.</summary>
    /// <param name="harness">The harness attached to the started host.</param>
    /// <param name="tag">A tag unique to this test, so the wait cannot match another suite's traffic.</param>
    /// <remarks>
    ///   - a publish at t=0 can go nowhere: unbound RabbitMQ queue, unassigned Kafka partition, late NATS consumer
    ///   - re-publishing until a copy lands replaces a fixed sleep
    ///   - duplicates carry the same headers
    /// </remarks>
    public async Task<RecordedMessage> PublishUntilConsumedAsync(MessagingTestHarness harness, string tag)
    {
        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            await harness.Bus.PublishAsync(new HarnessEvent { Tag = tag }, new PublishOptions { Headers = CallerHeaders });

            try
            {
                var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>(
                    match: e => e.Tag == tag,
                    timeout: AttemptTimeout);
                return consumed[0];
            }
            catch (TimeoutException) when (Stopwatch.GetElapsedTime(started) < StartupBudget)
            {
                // Subscription / topology not ready yet — publish again.
            }
        }
    }
}
