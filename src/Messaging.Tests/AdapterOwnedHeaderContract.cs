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
/// <para>
/// Regression cover for the bug where adapters stripped the whole <c>wt-</c> namespace on send while re-stamping only
/// the eight they own. Every reserved header belonging to an SDK <em>feature</em> — the second-level retry tier, the
/// dead-letter redrive count, the claim-check reference — was silently dropped at the broker, so those features worked
/// in-memory and were dead behind every real transport. <see cref="MessageHeaders.IsAdapterOwned"/> is the fix, and it
/// is called by five adapters; one broker proving the round trip left the other four uncovered.
/// </para>
/// <para>
/// One definition rather than one copy per broker, because a per-broker copy is free to drift: the value of asserting
/// through each feature's own reader (<see cref="SecondLevelRetryHeaders.ReadTier"/>,
/// <see cref="DeadLetterHeaders.ReadRedriveCount"/>) instead of raw strings is that the test dies when the *feature*
/// dies, and that property has to hold on all of them or the weakest copy is what the suite really asserts.
/// </para>
/// <para>
/// Every one of these needs a container: the in-memory transport hands the same envelope instance to the consumer and
/// never serializes headers, so it cannot observe a strip-on-send at all — which is exactly why the bug survived the
/// in-memory suite.
/// </para>
/// </remarks>
internal static class AdapterOwnedHeaderContract
{
    /// <summary>A type token nothing can resolve — what a caller forges onto an adapter-owned key.</summary>
    public const string ForgedEventType = "forged.contract.DoesNotExist";

    /// <summary>How long a broker gets to bring its subscription up before <see cref="PublishUntilConsumedAsync"/> gives up.</summary>
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(60);

    /// <summary>One publish attempt's share of that budget.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// What the caller hands the bus: two reserved-but-not-adapter-owned feature headers, an unreserved control, and a
    /// forgery attempt on an adapter-owned key.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CallerHeaders { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [SecondLevelRetryHeaders.Tier] = "2",
        [DeadLetterHeaders.RedriveCount] = "3",
        ["tenant-id"] = "acme", // unreserved: the easy case, and the control for the two above
        [MessageHeaders.EventType] = ForgedEventType, // adapter-owned — a forgery attempt
    };

    /// <summary>Assert both halves of the contract on a message that made the round trip through a real broker.</summary>
    /// <param name="consumed">The recorded consumption.</param>
    /// <param name="tag">The tag the message was published with.</param>
    public static void AssertRoundTrip(RecordedMessage consumed, string tag)
    {
        var received = consumed.Envelope.Headers;

        // The regression. Reserved but not adapter-owned → the adapter has no value of its own to write, so dropping
        // these is pure data loss and the feature that reads them downstream never fires.
        received.Should().ContainKey(SecondLevelRetryHeaders.Tier).WhoseValue.Should().Be("2");
        received.Should().ContainKey(DeadLetterHeaders.RedriveCount).WhoseValue.Should().Be("3");
        received.Should().ContainKey("tenant-id").WhoseValue.Should().Be("acme");

        // Asserted through each feature's own reader too: the header surviving as text is only half the promise —
        // what broke was second-level retry and the redrive cap reading 0 behind every broker.
        SecondLevelRetryHeaders.ReadTier(consumed.Envelope).Should().Be(2);
        DeadLetterHeaders.ReadRedriveCount(consumed.Envelope).Should().Be(3);

        // The other half: adapter-owned headers stay adapter-owned. The caller asked for a bogus type token; the
        // adapter overwrote it with the real one rather than letting the caller redirect type resolution.
        received.Should().ContainKey(MessageHeaders.EventType).WhoseValue.Should().Be(typeof(HarnessEvent).FullName);
        received[MessageHeaders.EventType].Should().NotBe(ForgedEventType);

        // Consumption at all is itself the proof: the receive side resolves the CLR type from wt-event-type, so had the
        // forgery stuck, reconstruction would have failed and nothing would ever have been recorded.
        consumed.BodyAs<HarnessEvent>().Tag.Should().Be(tag);
        received.Should().ContainKey(MessageHeaders.ContentType).WhoseValue.Should().Be("application/json");
    }

    /// <summary>Publish until the consumer records it, then return that record.</summary>
    /// <param name="harness">The harness attached to the started host.</param>
    /// <param name="tag">A tag unique to this test, so the wait cannot match another suite's traffic.</param>
    /// <remarks>
    /// <para>
    /// Each broker has its own reason a publish issued at t=0 can go nowhere: RabbitMQ's exchange discards a
    /// non-mandatory publish while no queue is bound, Kafka's consumer has not joined the group and been assigned a
    /// partition, and the NATS durable consumer is provisioned inside the hosted service rather than before
    /// <c>StartAsync</c> returns. Re-publishing until a copy lands is the condition-based form of the fixed sleep each
    /// would otherwise need; duplicates are harmless because every copy carries the same headers, which is all that is
    /// asserted.
    /// </para>
    /// <para>
    /// Redis Streams does not need this — its suite provisions the consumer group at
    /// <c>StreamPosition.Beginning</c> before publishing, which makes publish order irrelevant rather than merely
    /// unlikely to matter.
    /// </para>
    /// </remarks>
    public static async Task<RecordedMessage> PublishUntilConsumedAsync(MessagingTestHarness harness, string tag)
    {
        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            await harness.Bus.PublishAsync(new HarnessEvent(tag), new PublishOptions { Headers = CallerHeaders });

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
