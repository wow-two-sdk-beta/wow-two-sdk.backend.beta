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

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Tests;

/// <summary>
/// An <see cref="IBlobStorage"/> that keeps blobs in memory and records every call, so a test can assert both what was
/// stored and that nothing was stored at all.
/// </summary>
/// <remarks>
/// The local file store would serve the happy paths, but not the two assertions that matter most here: "the feature is
/// off, so the store was never touched" needs call counts, and "the blob is gone" needs a write that reports success
/// and keeps nothing — <see cref="DropWrites"/> — which is the retention-expired / purged shape without a sweep or a
/// sleep to produce it.
/// </remarks>
internal sealed class RecordingBlobStorage : IBlobStorage
{
    private readonly ConcurrentDictionary<string, BlobEntry> _blobs = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _calls = new();

    /// <summary>Accept writes and store nothing — the blob a later read cannot find.</summary>
    public bool DropWrites { get; set; }

    /// <summary>Every call made against this store, as <c>verb path</c>, in order.</summary>
    public IReadOnlyList<string> Calls => [.. _calls];

    /// <summary>How many blobs are actually held.</summary>
    public int BlobCount => _blobs.Count;

    /// <summary>Calls that read a blob or its metadata — what a rehydrate costs.</summary>
    public int ReadCalls => _calls.Count(call => call.StartsWith("GetInfo ", StringComparison.Ordinal) || call.StartsWith("OpenRead ", StringComparison.Ordinal));

    /// <summary>Calls that wrote a blob.</summary>
    public int SaveCalls => _calls.Count(call => call.StartsWith("Save ", StringComparison.Ordinal));

    /// <summary>The stored bytes at <paramref name="path"/>, or null when nothing is there.</summary>
    public byte[]? Read(string path) => _blobs.TryGetValue(path, out var entry) ? entry.Content : null;

    public Task SaveAsync(string path, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("Save " + path);
        if (DropWrites)
            return Task.CompletedTask;

        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _blobs[path] = new BlobEntry(buffer.ToArray(), contentType, DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("OpenRead " + path);
        return Task.FromResult<Stream?>(_blobs.TryGetValue(path, out var entry) ? new MemoryStream(entry.Content, writable: false) : null);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("Exists " + path);
        return Task.FromResult(_blobs.ContainsKey(path));
    }

    public Task<bool> DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("Delete " + path);
        return Task.FromResult(_blobs.TryRemove(path, out _));
    }

    public Task<BlobInfo?> GetInfoAsync(string path, CancellationToken cancellationToken = default)
    {
        _calls.Enqueue("GetInfo " + path);
        return Task.FromResult(_blobs.TryGetValue(path, out var entry)
            ? new BlobInfo { Path = path, SizeBytes = entry.Content.LongLength, LastModified = entry.LastModified, ContentType = entry.ContentType }
            : null);
    }

    public async IAsyncEnumerable<BlobInfo> ListAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // an in-memory listing has nothing to await; the iterator still has to be async

        _calls.Enqueue("List " + (prefix ?? string.Empty));
        foreach (var (path, entry) in _blobs)
        {
            if (prefix is not null && !path.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            yield return new BlobInfo { Path = path, SizeBytes = entry.Content.LongLength, LastModified = entry.LastModified, ContentType = entry.ContentType };
        }
    }

    private sealed record BlobEntry(byte[] Content, string? ContentType, DateTimeOffset LastModified);
}

/// <summary>
/// The claim-check pattern end to end: an oversized body is written to blob storage and a pointer travels instead, the
/// pointer is fetched back before the handler runs, and a pointer that cannot be honoured fails safely.
/// </summary>
/// <remarks>
/// <para>
/// Run against the in-memory transport, which hands the consumer the very envelope the bus built. That makes the wire
/// assertions read off <see cref="EventEnvelope.RawBody"/> and <see cref="EventEnvelope.WireBodyType"/> — the two fields
/// every adapter serializes from (<see cref="EventEnvelope.ToWireBody"/>) — rather than off bytes observed on a broker.
/// It also makes the rehydrate assertion a reference check: with the body arriving intact by construction, the only
/// proof the fetch happened is that the handler's body is a <i>different instance</i> that compares equal.
/// </para>
/// <para>
/// <see cref="ClaimCheckOptions.SweepEnabled"/> is off throughout. The retention sweeper is a background loop that
/// lists the prefix and deletes; leaving it on would race every assertion about what the store holds.
/// </para>
/// </remarks>
public sealed class ClaimCheckTests
{
    // Comfortably over the 4 KiB threshold below, and far enough over that no header or envelope field could account
    // for the difference when the wire body is asserted to be small.
    private const int ThresholdBytes = 4 * 1024;
    private static readonly string LargePayload = new('x', 16 * 1024);

    [Fact]
    public async Task Leaves_a_body_under_the_threshold_on_the_wire_and_never_touches_the_store()
    {
        var store = new RecordingBlobStorage();
        await using var harness = await StartAsync(store, claimCheck: true);

        await harness.Bus.PublishAsync(new HarnessEvent("small"));
        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>(match: e => e.Tag == "small");

        // The whole point of a threshold: under it the feature is inert on the wire.
        var published = harness.Published.Of<HarnessEvent>()[0].Envelope;
        published.Headers.Should().NotContainKey(ClaimCheckHeaders.Reference);
        published.Headers.Should().NotContainKey(ClaimCheckHeaders.Size);
        published.Headers.Should().NotContainKey(ClaimCheckHeaders.BodyType);
        published.RawBodyType.Should().BeNull();
        published.WireBodyType.Should().Be<HarnessEvent>();

        // RawBody IS set under the threshold — the offloader carries the bytes it measured so the adapter spends that
        // serialization instead of repeating it. Asserting they decode back to the same event is what makes carrying
        // them a pure optimization rather than a change to what travels.
        var serializer = harness.Services.GetRequiredService<IMessageSerializer>();
        published.RawBody.Should().NotBeNull();
        published.ToWireBody(serializer).Should().BeEquivalentTo(serializer.Serialize(new HarnessEvent("small"), typeof(HarnessEvent)));

        store.Calls.Should().BeEmpty();
        consumed[0].BodyAs<HarnessEvent>().Tag.Should().Be("small");
    }

    [Fact]
    public async Task Offloads_a_body_over_the_threshold_and_puts_a_small_reference_on_the_wire()
    {
        var store = new RecordingBlobStorage();
        await using var harness = await StartAsync(store, claimCheck: true);

        await harness.Bus.PublishAsync(new HarnessEvent(LargePayload));
        await harness.Consumed.WaitForAsync<HarnessEvent>();

        var published = harness.Published.Of<HarnessEvent>()[0].Envelope;
        var serializer = harness.Services.GetRequiredService<IMessageSerializer>();

        // One blob, under the configured prefix, holding exactly what the size header advertises.
        store.SaveCalls.Should().Be(1);
        store.BlobCount.Should().Be(1);
        var path = published.Headers[ClaimCheckHeaders.Reference];
        path.Should().StartWith("messaging/claim-check/");
        var stored = store.Read(path);
        stored.Should().NotBeNull();

        published.Headers[ClaimCheckHeaders.Size].Should().Be(stored!.LongLength.ToString(CultureInfo.InvariantCulture));
        published.Headers[ClaimCheckHeaders.BodyType].Should().Be(typeof(HarnessEvent).FullName);

        // The substitution: the bytes and the type token that travel are the reference's, while BodyType — what routing
        // and metrics read — stays the real contract, so the message still lands where its consumers are bound.
        published.WireBodyType.Should().Be<ClaimCheckReference>();
        published.RawBodyType.Should().Be<ClaimCheckReference>();
        published.BodyType.Should().Be<HarnessEvent>();

        var wire = published.ToWireBody(serializer);
        wire.Length.Should().BeLessThan(ThresholdBytes); // the reason the feature exists: the broker sees a small message
        stored.LongLength.Should().BeGreaterThan(ThresholdBytes);

        var reference = (ClaimCheckReference)serializer.Deserialize(wire, typeof(ClaimCheckReference))!;
        reference.Path.Should().Be(path);
        reference.SizeBytes.Should().Be(stored.LongLength);
        reference.ContentType.Should().Be(serializer.ContentType);
        reference.BodyType.Should().Be(typeof(HarnessEvent).FullName);
    }

    [Fact]
    public async Task Never_calls_the_blob_store_when_the_feature_was_not_registered()
    {
        var store = new RecordingBlobStorage();
        await using var harness = await StartAsync(store, claimCheck: false);

        var sent = new HarnessEvent(LargePayload);
        await harness.Bus.PublishAsync(sent);
        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>();

        // Not "no blob written" — no call of any kind. Without AddEventClaimCheck() nothing resolves the offloader, so
        // the send path is the one that existed before the feature did.
        store.Calls.Should().BeEmpty();

        var published = harness.Published.Of<HarnessEvent>()[0].Envelope;
        published.RawBody.Should().BeNull();
        published.Headers.Should().NotContainKey(ClaimCheckHeaders.Reference);

        // Same instance the test published — this transport passes the envelope by reference when nothing intercepts
        // it. It is also the negative control for the rehydrate test above: that one asserts NOT-same-instance, which
        // only discriminates because this line shows same-instance is what an unintercepted body actually looks like.
        consumed[0].BodyAs<HarnessEvent>().Should().BeSameAs(sent);
    }

    [Fact]
    public async Task Rehydrates_the_real_body_before_the_handler_sees_it()
    {
        var store = new RecordingBlobStorage();
        await using var harness = await StartAsync(store, claimCheck: true);

        var sent = new HarnessEvent(LargePayload);
        await harness.Bus.PublishAsync(sent);
        var consumed = await harness.Consumed.WaitForAsync<HarnessEvent>();

        // The handler was dispatched the contract, never the pointer.
        var body = consumed[0].BodyAs<HarnessEvent>();
        body.Tag.Should().Be(LargePayload);
        consumed[0].Envelope.BodyType.Should().Be<HarnessEvent>();
        consumed[0].Body.Should().NotBeOfType<ClaimCheckReference>();

        // The proof the round trip actually went through storage. This transport hands the consumer the same envelope
        // instance the bus built, so an equal body proves nothing on its own — a body that is equal but NOT the same
        // instance can only have come back out of the blob.
        body.Should().NotBeSameAs(sent);
        body.Should().Be(sent);
        store.ReadCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Dead_letters_a_missing_blob_without_spending_the_retry_budget()
    {
        var store = new RecordingBlobStorage { DropWrites = true }; // the write reports success and keeps nothing
        await using var harness = await StartAsync(store, claimCheck: true, retry: new RetryConfig(MaxAttempts: 5, Backoff: BackoffKind.None));

        await harness.Bus.PublishAsync(new HarnessEvent(LargePayload));
        var deadLettered = await harness.DeadLettered.WaitForAsync<HarnessEvent>();

        deadLettered[0].Exception.Should().BeOfType<ClaimCheckPayloadException>();
        deadLettered[0].Exception!.Message.Should().Contain("missing from blob storage"); // the reason an operator triages on

        // The claim: the rehydrate filter throws BEFORE calling next, so the resilience pipeline — which lives inside
        // the core the filter wraps — never runs. No attempt is spent on a fault that redelivery cannot fix.
        harness.Faulted.Count<HarnessEvent>().Should().Be(0);
        harness.Consumed.Count<HarnessEvent>().Should().Be(0);

        // The control that keeps the two lines above from passing vacuously: on this same host, with this same retry
        // schedule, a fault raised INSIDE the core does spend the budget and the recorder does see every attempt.
        await harness.Bus.PublishAsync(new BoomEvent("control"));
        await harness.DeadLettered.WaitForAsync<BoomEvent>();
        harness.Faulted.Count<BoomEvent>().Should().Be(5);
    }

    [Theory]
    [InlineData("tenant-secrets/credentials.json", "points outside")] // valid path, wrong prefix — the confinement check
    [InlineData("messaging/claim-check/../../etc/passwd", "not a valid blob path")] // traversal — rejected at normalization
    public async Task Refuses_a_forged_reference_that_points_outside_the_prefix(string forgedPath, string expectedReason)
    {
        var store = new RecordingBlobStorage();
        await using var harness = await StartAsync(store, claimCheck: true);

        // A small body, so nothing was offloaded and the reference is purely the attacker's. It rides through because
        // the in-memory transport does not strip caller headers — which is the position a compromised or buggy producer
        // puts a real consumer in behind any broker that carries reserved headers through.
        await harness.Bus.PublishAsync(
            new HarnessEvent("forged"),
            new PublishOptions { Headers = new Dictionary<string, string>(StringComparer.Ordinal) { [ClaimCheckHeaders.Reference] = forgedPath } });

        var deadLettered = await harness.DeadLettered.WaitForAsync<HarnessEvent>();
        deadLettered[0].Exception.Should().BeOfType<ClaimCheckPayloadException>();
        deadLettered[0].Exception!.Message.Should().Contain(expectedReason);

        // Refused before the store, not after: the guard must not become a read of an arbitrary blob whose result is
        // then discarded, because on a shared store that read is the disclosure.
        store.ReadCalls.Should().Be(0);
        harness.Consumed.Count<HarnessEvent>().Should().Be(0);
    }

    [Fact]
    public async Task Keeps_the_blob_after_a_dead_letter_so_a_redrive_can_rehydrate_it()
    {
        var store = new RecordingBlobStorage();
        await using var harness = await StartAsync(store, claimCheck: true, retry: new RetryConfig(MaxAttempts: 5, Backoff: BackoffKind.None));

        await harness.Bus.PublishAsync(new BoomEvent(LargePayload)); // offloaded, rehydrated, then the handler throws
        var deadLettered = await harness.DeadLettered.WaitForAsync<BoomEvent>();

        // Consuming never deletes. A fan-out sibling, the next retry and a redrive days later all read this same blob,
        // so the only thing that may remove it is the retention sweep.
        store.BlobCount.Should().Be(1);
        store.Calls.Should().NotContain(call => call.StartsWith("Delete ", StringComparison.Ordinal));

        // What the dead-letter record holds is the pointer, not the payload — which is what a redrive follows and what
        // keeps the dead-letter store from growing by the size of every oversized body.
        var record = deadLettered[0].Envelope;
        record.Headers.Should().ContainKey(ClaimCheckHeaders.Reference);
        store.Read(record.Headers[ClaimCheckHeaders.Reference]).Should().NotBeNull();

        // The rehydrate is a filter wrapping the core, and the retry loop lives inside that core: the body is fetched
        // once and every attempt reuses it, rather than re-reading the blob per attempt.
        harness.Faulted.Count<BoomEvent>().Should().Be(5);
        store.Calls.Count(call => call.StartsWith("OpenRead ", StringComparison.Ordinal)).Should().Be(1);
    }

    private static Task<MessagingTestHarness> StartAsync(RecordingBlobStorage store, bool claimCheck, RetryConfig? retry = null)
        => MessagingTestHarness.StartAsync(
            services =>
            {
                services.AddSingleton<EventCollector>(); // PingHandler is scanned from this assembly and needs one
                services.AddSingleton<IBlobStorage>(store);
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
