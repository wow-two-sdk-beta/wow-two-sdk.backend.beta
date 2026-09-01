using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Testing.Messaging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// The claim-check pattern end to end: an oversized body is written to blob storage and a pointer travels instead, the
/// pointer is fetched back before the handler runs, and a pointer that cannot be honoured fails safely.
/// </summary>
/// <remarks>
///   - runs on the in-memory transport, so wire assertions read <see cref="EventEnvelope.RawBody"/> and <see cref="EventEnvelope.WireBodyType"/>
///   - the body arrives intact, so only a different-but-equal instance proves the rehydrate fetch ran
///   - <see cref="ClaimCheckOptions.SweepEnabled"/> stays off — the sweeper would race every assertion about the store
/// </remarks>
public sealed class ClaimCheckTests
{
    // LargePayload sits far enough over the threshold that no header or envelope field explains a small wire body.
    private const int ThresholdBytes = 4 * 1024;
    private static readonly string LargePayload = new('x', 16 * 1024);

    [Fact]
    public async Task Leaves_a_body_under_the_threshold_on_the_wire_and_never_touches_the_store()
    {
        var store = new RecordingBlobRepository();
        await using var harness = await StartAsync(store, claimCheck: true);

        await harness.Bus.PublishAsync(new HarnessEvent("small"));
        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>(match: e => e.Tag == "small");

        // The whole point of a threshold: under it the feature is inert on the wire.
        var published = harness.Published.Of<HarnessEvent>()[0].Envelope;
        published.Headers.Should().NotContainKey(ClaimCheckHeaderConstants.Reference);
        published.Headers.Should().NotContainKey(ClaimCheckHeaderConstants.Size);
        published.Headers.Should().NotContainKey(ClaimCheckHeaderConstants.BodyType);
        published.RawBodyType.Should().BeNull();
        published.WireBodyType.Should().Be<HarnessEvent>();

        // RawBody IS set under the threshold: the offloader carries the bytes it measured so the adapter re-uses them.
        var serializer = harness.Services.GetRequiredService<IMessageSerializer>();
        published.RawBody.Should().NotBeNull();
        published.ToWireBody(serializer).Should().BeEquivalentTo(serializer.Serialize(new HarnessEvent("small"), typeof(HarnessEvent)));

        store.Calls.Should().BeEmpty();
        consumed[0].BodyAs<HarnessEvent>().Tag.Should().Be("small");
    }

    [Fact]
    public async Task Offloads_a_body_over_the_threshold_and_puts_a_small_reference_on_the_wire()
    {
        var store = new RecordingBlobRepository();
        await using var harness = await StartAsync(store, claimCheck: true);

        await harness.Bus.PublishAsync(new HarnessEvent(LargePayload));
        await harness.Consumed.WaitForAsync<HarnessEvent>();

        var published = harness.Published.Of<HarnessEvent>()[0].Envelope;
        var serializer = harness.Services.GetRequiredService<IMessageSerializer>();

        // One blob, under the configured prefix, holding exactly what the size header advertises.
        store.SaveCalls.Should().Be(1);
        store.BlobCount.Should().Be(1);
        var path = published.Headers[ClaimCheckHeaderConstants.Reference];
        path.Should().StartWith("messaging/claim-check/");
        var stored = store.Read(path);
        stored.Should().NotBeNull();

        published.Headers[ClaimCheckHeaderConstants.Size].Should().Be(stored!.LongLength.ToString(CultureInfo.InvariantCulture));
        published.Headers[ClaimCheckHeaderConstants.BodyType].Should().Be(typeof(HarnessEvent).FullName);

        // The substitution: the reference travels, while BodyType keeps routing on the real contract.
        published.WireBodyType.Should().Be<ClaimCheckReference>();
        published.RawBodyType.Should().Be<ClaimCheckReference>();
        published.BodyType.Should().Be<HarnessEvent>();

        var wire = published.ToWireBody(serializer);
        wire.Length.Should().BeLessThan(ThresholdBytes); // the reason the feature exists: the broker sees a small message
        stored.LongLength.Should().BeGreaterThan(ThresholdBytes);

        var reference = (ClaimCheckReference)serializer.Deserialize(wire, typeof(ClaimCheckReference)).ValueOrThrow();
        reference.Path.Should().Be(path);
        reference.SizeBytes.Should().Be(stored.LongLength);
        reference.ContentType.Should().Be(serializer.ContentType);
        reference.BodyType.Should().Be(typeof(HarnessEvent).FullName);
    }

    [Fact]
    public async Task Never_calls_the_blob_store_when_the_feature_was_not_registered()
    {
        var store = new RecordingBlobRepository();
        await using var harness = await StartAsync(store, claimCheck: false);

        var sent = new HarnessEvent(LargePayload);
        await harness.Bus.PublishAsync(sent);
        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>();

        // Without AddEventClaimCheck() nothing resolves the offloader, so the store sees no call of any kind.
        store.Calls.Should().BeEmpty();

        var published = harness.Published.Of<HarnessEvent>()[0].Envelope;
        published.RawBody.Should().BeNull();
        published.Headers.Should().NotContainKey(ClaimCheckHeaderConstants.Reference);

        // This transport passes the envelope by reference when nothing intercepts it, so the instance is unchanged.
        consumed[0].BodyAs<HarnessEvent>().Should().BeSameAs(sent);
    }

    [Fact]
    public async Task Rehydrates_the_real_body_before_the_handler_sees_it()
    {
        var store = new RecordingBlobRepository();
        await using var harness = await StartAsync(store, claimCheck: true);

        var sent = new HarnessEvent(LargePayload);
        await harness.Bus.PublishAsync(sent);
        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>();

        // The handler was dispatched the contract, never the pointer.
        var body = consumed[0].BodyAs<HarnessEvent>();
        body.Tag.Should().Be(LargePayload);
        consumed[0].Envelope.BodyType.Should().Be<HarnessEvent>();
        consumed[0].Body.Should().NotBeOfType<ClaimCheckReference>();

        // Equal but NOT the same instance is the only proof the body came back out of the blob.
        body.Should().NotBeSameAs(sent);
        body.Should().Be(sent);
        store.ReadCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Dead_letters_a_missing_blob_without_spending_the_retry_budget()
    {
        var store = new RecordingBlobRepository { DropWrites = true }; // the write reports success and keeps nothing
        await using var harness = await StartAsync(store, claimCheck: true, retry: new RetryConfig(MaxAttempts: 5, Backoff: BackoffKind.None));

        await harness.Bus.PublishAsync(new HarnessEvent(LargePayload));
        var deadLettered = await harness.DeadLettered.WaitForAsync<HarnessEvent>();

        deadLettered[0].Exception.Should().BeOfType<ClaimCheckPayloadException>();
        deadLettered[0].Exception!.Message.Should().Contain("missing from blob storage"); // the reason an operator triages on

        // The rehydrate filter throws BEFORE calling next, so the resilience pipeline inside the core never runs.
        harness.Faulted.Count<HarnessEvent>().Should().Be(0);
        harness.Consumed.Count<HarnessEvent>().Should().Be(0);

        // The control: on the same host and schedule, a fault raised INSIDE the core does spend the budget.
        await harness.Bus.PublishAsync(new BoomEvent("control"));
        await harness.DeadLettered.WaitForAsync<BoomEvent>();
        harness.Faulted.Count<BoomEvent>().Should().Be(5);
    }

    [Theory]
    [InlineData("tenant-secrets/credentials.json", "points outside")] // valid path, wrong prefix — the confinement check
    [InlineData("messaging/claim-check/../../etc/passwd", "not a valid blob path")] // traversal — rejected at normalization
    public async Task Refuses_a_forged_reference_that_points_outside_the_prefix(string forgedPath, string expectedReason)
    {
        var store = new RecordingBlobRepository();
        await using var harness = await StartAsync(store, claimCheck: true);

        // A small body, so the reference is purely the attacker's: this transport does not strip caller headers.
        await harness.Bus.PublishAsync(
            new HarnessEvent("forged"),
            new PublishOptions { Headers = new Dictionary<string, string>(StringComparer.Ordinal) { [ClaimCheckHeaderConstants.Reference] = forgedPath } });

        var deadLettered = await harness.DeadLettered.WaitForAsync<HarnessEvent>();
        deadLettered[0].Exception.Should().BeOfType<ClaimCheckPayloadException>();
        deadLettered[0].Exception!.Message.Should().Contain(expectedReason);

        // The guard refuses before the store — on a shared store, reading an arbitrary blob is the disclosure.
        store.ReadCalls.Should().Be(0);
        harness.Consumed.Count<HarnessEvent>().Should().Be(0);
    }

    [Fact]
    public async Task Keeps_the_blob_after_a_dead_letter_so_a_redrive_can_rehydrate_it()
    {
        var store = new RecordingBlobRepository();
        await using var harness = await StartAsync(store, claimCheck: true, retry: new RetryConfig(MaxAttempts: 5, Backoff: BackoffKind.None));

        await harness.Bus.PublishAsync(new BoomEvent(LargePayload)); // offloaded, rehydrated, then the handler throws
        var deadLettered = await harness.DeadLettered.WaitForAsync<BoomEvent>();

        // Consuming never deletes: a fan-out sibling, the next retry and a later redrive read this same blob.
        store.BlobCount.Should().Be(1);
        store.Calls.Should().NotContain(call => call.StartsWith("Delete ", StringComparison.Ordinal));

        // The dead-letter record holds the pointer a redrive follows, never the payload.
        var record = deadLettered[0].Envelope;
        record.Headers.Should().ContainKey(ClaimCheckHeaderConstants.Reference);
        store.Read(record.Headers[ClaimCheckHeaderConstants.Reference]).Should().NotBeNull();

        // The retry loop lives inside the wrapped core, so the body is fetched once and every attempt reuses it.
        harness.Faulted.Count<BoomEvent>().Should().Be(5);
        store.Calls.Count(call => call.StartsWith("OpenRead ", StringComparison.Ordinal)).Should().Be(1);
    }

    private static Task<MessagingTestHarness> StartAsync(RecordingBlobRepository store, bool claimCheck, RetryConfig? retry = null)
        => MessagingTestHarness.StartAsync(
            services =>
            {
                services.AddScannedHandlerDependencies(); // PingHandler is scanned from this assembly and needs one
                services.AddSingleton<IBlobRepository>(store);
                if (claimCheck)
                {
                    services.AddEventClaimCheck(options =>
                    {
                        options.ThresholdBytes = ThresholdBytes;
                        options.SweepEnabled = false; // a background sweep would race every assertion about the store
                    });
                }
            },
            configureBus: retry is null ? null : options => options.Retry = retry,
            handlerAssemblies: [typeof(HarnessHandler).Assembly]);
}
