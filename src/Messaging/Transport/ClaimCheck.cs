using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>Reserved wire headers carrying a message's claim check — the pointer to a body held in blob storage rather than on the wire.</summary>
/// <remarks>
/// Defined here rather than in <see cref="MessageHeaders"/> for the same reason
/// <see cref="Reliability.SecondLevelRetryHeaders"/> and <see cref="Reliability.DeadLetterHeaders"/> are: a feature that
/// is off by default owns its own wire names, and the shared table stays the set every adapter must understand.
/// </remarks>
public static class ClaimCheckHeaders
{
    /// <summary>Reserved. Logical blob path of the offloaded body. Its presence is what marks a message as claim-checked.</summary>
    public const string Reference = MessageHeaders.ReservedPrefix + "claim-check";

    /// <summary>Reserved. Size in bytes of the offloaded body, for observability and for spotting a truncated blob.</summary>
    public const string Size = MessageHeaders.ReservedPrefix + "claim-check-size";

    /// <summary>
    /// Reserved. Wire token of the <i>real</i> body type — the one the offloaded blob deserializes into.
    /// </summary>
    /// <remarks>
    /// Load-bearing, not an optimization. An offloaded message puts a <see cref="ClaimCheckReference"/> on the wire, so
    /// <see cref="MessageHeaders.EventType"/> names <i>that</i> type (it is what the receiving adapter must decode the
    /// bytes as) and the real contract has nowhere else to travel. The rehydrator reads it, and falls back to the
    /// envelope's own <see cref="EventEnvelope.BodyType"/> only for a transport that never re-encoded the body at all —
    /// the in-memory one, which hands the envelope over by reference.
    /// </remarks>
    public const string BodyType = MessageHeaders.ReservedPrefix + "claim-check-type";

    /// <summary>Read the claim reference off an envelope.</summary>
    /// <param name="envelope">The envelope to inspect.</param>
    /// <param name="path">The blob path, when the message carries one.</param>
    /// <returns><see langword="true"/> when the body is offloaded and must be rehydrated before dispatch.</returns>
    public static bool TryReadReference(EventEnvelope? envelope, out string path)
    {
        if (envelope?.Headers is { Count: > 0 } headers
            && headers.TryGetValue(Reference, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            path = value;
            return true;
        }

        path = string.Empty;
        return false;
    }
}

/// <summary>
/// The claim check itself — what travels on the wire in place of a body too large for the broker. Small, fixed-size,
/// and enough to fetch the real body back: where it is stored, how big it was, and how it was serialized.
/// </summary>
/// <remarks>
/// Deliberately not an <see cref="IEvent"/>: it is a wire artefact, not a business fact, and must never be publishable
/// through <see cref="IEventBus"/> or discoverable by a handler scan.
/// </remarks>
public sealed record ClaimCheckReference
{
    /// <summary>
    /// Stable wire token this type is registered under by <c>AddEventClaimCheck</c>, so a consumer resolves the wire
    /// body of an offloaded message without depending on the SDK's assembly-qualified name — whose version segment
    /// moves on every SDK push, and which would leave the message unresolvable across a version skew.
    /// </summary>
    public const string TypeToken = "wt.claim-check-reference";

    /// <summary>Logical blob path the body was written to, inside the configured <see cref="ClaimCheckOptions.PathPrefix"/>.</summary>
    public required string Path { get; init; }

    /// <summary>Size in bytes of the serialized body that was offloaded.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Content type the body was serialized with — checked on rehydrate so a serializer swap fails loudly rather than mis-decoding.</summary>
    public required string ContentType { get; init; }

    /// <summary>Wire token of the real body type; <see langword="null"/> when the wire already carries it as <c>wt-event-type</c>.</summary>
    public string? BodyType { get; init; }
}

/// <summary>
/// Move a body too large for the broker into blob storage and send a pointer instead, rehydrating it before the handler
/// sees it. Every broker caps message size (Azure Service Bus Standard and SQS at 256 KB, Kafka at 1 MB by default), and
/// a body over the cap is rejected at publish with nothing the SDK can do about it.
/// </summary>
/// <remarks>
/// <para>
/// Off by default (<see cref="Enabled"/> = <c>false</c>). Nothing is consulted, no blob is written and the consume filter
/// short-circuits on the first header lookup until <c>AddEventClaimCheck(...)</c> is called, so an application that
/// configures nothing puts the same bytes on the wire as before.
/// </para>
/// <para>
/// <b>Blobs are never deleted on consume — only by retention.</b> Three things would break if they were.
/// A published event fans out to every subscriber, so the first consumer to finish would delete the blob out from under
/// the others. A retry re-reads the same blob, so deleting on the attempt that failed makes every later attempt fail
/// permanently. And a dead-lettered message's record stores the pointer, so a redrive (<see cref="Reliability.IDeadLetterAdmin"/>)
/// days later needs the blob still there. <see cref="Retention"/> is therefore the real redrive window: set it longer
/// than the retry ladder plus however long dead-letter triage takes, or a redrive rehydrates nothing.
/// </para>
/// <para>
/// Storage rides the SDK's existing <see cref="IBlobStorage"/> vector — register one (<c>AddLocalBlobStorage</c> or a
/// cloud adapter) alongside this. On a store with its own lifecycle rules (S3 lifecycle, Azure blob lifecycle
/// management) prefer those and set <see cref="SweepEnabled"/> to <c>false</c>: they expire objects server-side instead
/// of listing the whole prefix on a timer.
/// </para>
/// </remarks>
public sealed class ClaimCheckOptions
{
    /// <summary>
    /// Offload bodies over <see cref="ThresholdBytes"/> instead of sending them inline. Defaults to <c>false</c>;
    /// <c>AddEventClaimCheck(...)</c> turns it on before applying the caller's configuration, so a bound configuration
    /// section that omits the key cannot switch the wire format over by accident — but can still switch that call back off.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Serialized-body size above which the body is offloaded. Default 128 KiB — under the tightest common broker cap
    /// (256 KB) with room left for headers, so a message that passes the check also passes the broker.
    /// </summary>
    public int ThresholdBytes { get; set; } = 128 * 1024;

    /// <summary>Logical blob path prefix every claim-checked body is written under. A reference pointing outside it is refused on rehydrate.</summary>
    public string PathPrefix { get; set; } = "messaging/claim-check";

    /// <summary>
    /// How long an offloaded body is kept before the sweep deletes it. Default 7 days. This is the redrive window — a
    /// dead-lettered message replayed after it expires cannot be rehydrated and dead-letters again.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>Run the retention sweep in-process. Default <c>true</c>; turn it off when the blob store expires objects itself.</summary>
    public bool SweepEnabled { get; set; } = true;

    /// <summary>How often the retention sweep runs. Default 1 hour.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Largest blob the rehydrator will read back. Default 64 MiB. A reference is wire data and may be corrupt or
    /// hostile; without a ceiling one bad pointer at a huge object takes the consumer down with an out-of-memory.
    /// </summary>
    public long MaxPayloadBytes { get; set; } = 64L * 1024 * 1024;
}

/// <summary>
/// An offloaded body could not be read back, so the message cannot be handled — the blob is gone, unreadable, or the
/// reference does not point at something this consumer is willing to fetch. Terminal: a redelivery reads the same
/// reference and fails the same way.
/// </summary>
public sealed class ClaimCheckPayloadException : Exception
{
    /// <summary>Create the exception.</summary>
    public ClaimCheckPayloadException()
        : base("The claim-checked body could not be read back from blob storage.")
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message, which becomes the dead-letter reason.</param>
    public ClaimCheckPayloadException(string message)
        : base(message)
    {
    }

    /// <summary>Create the exception.</summary>
    /// <param name="message">The message, which becomes the dead-letter reason.</param>
    /// <param name="innerException">The underlying storage or deserialization failure.</param>
    public ClaimCheckPayloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Reads and writes claim-checked bodies through the SDK's <see cref="IBlobStorage"/>, owning the path scheme and the guards on a reference that arrived over the wire.</summary>
internal sealed class ClaimCheckPayloadStore(IBlobStorage blobStorage, IOptions<ClaimCheckOptions> options, TimeProvider timeProvider)
{
    private readonly ClaimCheckOptions _options = options.Value;

    /// <summary>The normalized prefix every claim-checked blob lives under, with no trailing separator.</summary>
    public string Prefix { get; } = BlobStoragePath.Normalize(options.Value.PathPrefix).TrimEnd('/');

    /// <summary>Write a serialized body to blob storage and return the logical path to reference it by.</summary>
    /// <param name="body">The serialized body.</param>
    /// <param name="contentType">The serializer's content type, recorded where the backing store supports it.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<string> WriteAsync(byte[] body, string contentType, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Date segments so a store with prefix-scoped lifecycle rules can expire whole days, and so no single directory
        // grows without bound. The name is a fresh GUID rather than the message id: an id is caller-supplied, may repeat
        // across a redrive, and may contain characters that have meaning in a path.
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"{Prefix}/{now:yyyy'/'MM'/'dd}/{Guid.NewGuid():N}.bin");

        using var stream = new MemoryStream(body, writable: false);
        await blobStorage.SaveAsync(path, stream, contentType, cancellationToken);
        return path;
    }

    /// <summary>Read an offloaded body back, or throw <see cref="ClaimCheckPayloadException"/> with a reason fit to dead-letter on.</summary>
    /// <param name="path">The claim reference, as it arrived on the wire.</param>
    /// <param name="messageId">The message the reference came from, for the failure message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<byte[]> ReadAsync(string path, string messageId, CancellationToken cancellationToken)
    {
        var safePath = Confine(path, messageId);

        // Metadata first: it separates "gone" from "too big" before a byte is allocated, and gives the exact length to
        // read into. Two round trips on the offloaded path only — the cheap path never gets here.
        var info = await blobStorage.GetInfoAsync(safePath, cancellationToken);
        if (info is null)
            throw Missing(safePath, messageId);

        if (info.SizeBytes > _options.MaxPayloadBytes)
        {
            throw new ClaimCheckPayloadException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Claim-checked body '{safePath}' for message '{messageId}' is {info.SizeBytes} bytes, over the {_options.MaxPayloadBytes}-byte rehydrate limit; refusing to load it."));
        }

        var stream = await blobStorage.OpenReadAsync(safePath, cancellationToken);
        if (stream is null)
            throw Missing(safePath, messageId); // deleted between the two calls — a retention sweep racing a slow consumer

        await using (stream)
        {
            var buffer = new byte[info.SizeBytes];
            try
            {
                await stream.ReadExactlyAsync(buffer, cancellationToken);
            }
            catch (EndOfStreamException exception)
            {
                throw new ClaimCheckPayloadException(
                    $"Claim-checked body '{safePath}' for message '{messageId}' is shorter than its recorded length; the blob is truncated.",
                    exception);
            }

            return buffer;
        }
    }

    /// <summary>Delete every claim-checked blob last modified before <paramref name="cutoffUtc"/>, up to <paramref name="maxDeletes"/> in one pass.</summary>
    /// <param name="cutoffUtc">Blobs older than this are expired.</param>
    /// <param name="maxDeletes">Ceiling on one pass, so a first sweep over a large store does not run unbounded.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many blobs were deleted.</returns>
    public async ValueTask<int> SweepAsync(DateTimeOffset cutoffUtc, int maxDeletes, CancellationToken cancellationToken)
    {
        // Collected before deleting: mutating the store while its own listing is being enumerated is undefined on some
        // backends and throws outright on a directory-backed one.
        var expired = new List<string>();
        await foreach (var blob in blobStorage.ListAsync(Prefix, cancellationToken))
        {
            if (blob.LastModified >= cutoffUtc)
                continue;

            expired.Add(blob.Path);
            if (expired.Count >= maxDeletes)
                break; // the rest keep until the next pass
        }

        var deleted = 0;
        foreach (var blobPath in expired)
            if (await blobStorage.DeleteAsync(blobPath, cancellationToken))
                deleted++;

        return deleted;
    }

    // A reference is wire data, so it is attacker-reachable: normalize it (which rejects traversal segments) and then
    // confine it to the configured prefix, so a forged header cannot turn the rehydrator into a reader for arbitrary
    // blobs — credentials, other tenants' payloads — that happen to share the store.
    private string Confine(string path, string messageId)
    {
        string normalized;
        try
        {
            normalized = BlobStoragePath.Normalize(path);
        }
        catch (ArgumentException exception)
        {
            throw new ClaimCheckPayloadException($"Claim-check reference '{path}' on message '{messageId}' is not a valid blob path.", exception);
        }

        if (!normalized.StartsWith(Prefix + "/", StringComparison.Ordinal))
            throw new ClaimCheckPayloadException($"Claim-check reference '{path}' on message '{messageId}' points outside the '{Prefix}' prefix; refusing to read it.");

        return normalized;
    }

    private static ClaimCheckPayloadException Missing(string path, string messageId) => new(
        $"Claim-checked body '{path}' for message '{messageId}' is missing from blob storage — it expired under the configured retention, was purged, or was never written. The body cannot be rehydrated.");
}

/// <summary>
/// The send half: serializes the body once, and where it exceeds the threshold writes it to blob storage and puts the
/// small reference on the wire in its place.
/// </summary>
/// <remarks>
/// <para>
/// Called by <see cref="TransportEventBus"/> on every publish, before the envelope reaches an adapter, and it works
/// through <see cref="EventEnvelope.RawBody"/> rather than by decorating something. An <see cref="ISendTransport"/>
/// decorator would have to swap <see cref="EventEnvelope.BodyType"/> to change the wire body, which re-routes the
/// message to a key nothing is bound for; an <see cref="IMessageSerializer"/> decorator has a synchronous API, so blob
/// I/O would run sync-over-async on the consume pump, and it cannot stamp a header.
/// </para>
/// <para>
/// The bytes replacing the body are a serialized <see cref="ClaimCheckReference"/>, so
/// <see cref="EventEnvelope.RawBodyType"/> names that type and the receiving adapter decodes what is actually there.
/// <see cref="EventEnvelope.BodyType"/> is left alone, so <see cref="ITopologyProvider.ResolveRoutingKey"/> still routes
/// by the real contract and the message reaches the consumers bound to it.
/// </para>
/// <para>
/// Under the threshold the envelope still carries the bytes that were measured, so an enabled claim check costs one
/// serialization per message rather than two — the adapter sends what the size check produced instead of producing it
/// again.
/// </para>
/// </remarks>
internal sealed partial class ClaimCheckOffloader(
    ClaimCheckPayloadStore store,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    IOptions<ClaimCheckOptions> options,
    ILogger<ClaimCheckOffloader> logger)
{
    private readonly ClaimCheckOptions _options = options.Value;

    /// <summary>True when oversized bodies are offloaded. False leaves the send path putting every body inline, exactly as before this existed.</summary>
    public bool IsActive => _options.Enabled;

    /// <summary>
    /// Decide what <paramref name="envelope"/> puts on the wire: the body's own bytes, or — over the threshold — a
    /// reference to them in blob storage. Returns the same instance untouched whenever there is nothing to do, so the
    /// caller can assign the result unconditionally.
    /// </summary>
    /// <param name="envelope">The envelope about to be sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<EventEnvelope> PrepareAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // Off, or an earlier transformation already fixed the wire bytes and owns what travels.
        if (!IsActive || envelope.RawBody is not null)
            return envelope;

        // A re-publish of an already-offloaded message — a delayed-retry hop, a dead-letter redrive — carries the
        // pointer rather than the payload. Offloading again would store a blob whose content is a reference to a blob,
        // and the rehydrator would hand the handler a ClaimCheckReference. The header survives such a re-publish
        // precisely because MessageHeaders.IsAdapterOwned strips only the keys an adapter re-derives, so this is where
        // the loop has to be cut.
        if (ClaimCheckHeaders.TryReadReference(envelope, out _))
            return envelope;

        // Size is not knowable without serializing. Carrying the measured bytes on the envelope is what keeps that to
        // one serialization per message: under the threshold they are the wire body, over it they are what gets stored.
        var body = serializer.Serialize(envelope.Body, envelope.BodyType);
        if (body.LongLength <= _options.ThresholdBytes)
            return envelope with { RawBody = new ReadOnlyMemory<byte>(body) };

        var bodyToken = typeResolver.ToTypeToken(envelope.BodyType);
        var path = await store.WriteAsync(body, serializer.ContentType, cancellationToken);
        var reference = new ClaimCheckReference
        {
            Path = path,
            SizeBytes = body.LongLength,
            ContentType = serializer.ContentType,
            BodyType = bodyToken,
        };

        // Merged into the envelope's own headers rather than handed to the adapter separately: an adapter copies
        // caller headers minus MessageHeaders.IsAdapterOwned, which covers only the 8 keys it re-derives, so these
        // three reach the wire with no adapter-side header work at all.
        var headers = new Dictionary<string, string>(envelope.Headers, StringComparer.Ordinal)
        {
            [ClaimCheckHeaders.Reference] = path,
            [ClaimCheckHeaders.Size] = reference.SizeBytes.ToString(CultureInfo.InvariantCulture),
            [ClaimCheckHeaders.BodyType] = bodyToken,
        };

        LogOffloaded(envelope.MessageId, reference.SizeBytes, path);

        // Body and BodyType stay the real event: routing, metrics and observers keep describing what was published,
        // and only the bytes and the type token that travel with them are substituted.
        return envelope with
        {
            RawBody = new ReadOnlyMemory<byte>(serializer.Serialize(reference, typeof(ClaimCheckReference))),
            RawBodyType = typeof(ClaimCheckReference),
            Headers = headers,
        };
    }

    [LoggerMessage(EventId = 6081, Level = LogLevel.Debug, Message = "Offloaded the {SizeBytes}-byte body of message {MessageId} to claim check {Path}")]
    private partial void LogOffloaded(string messageId, long sizeBytes, string path);
}

/// <summary>
/// The consume half: fetches an offloaded body back out of blob storage and swaps it into the envelope, so a handler is
/// dispatched the real event and never learns the body travelled separately.
/// </summary>
/// <remarks>
/// <para>
/// A filter rather than a pipeline change, on the same reasoning as second-level retry: the rehydrate is a concern that
/// wraps dispatch, and a message with no claim-check header falls through on one dictionary lookup.
/// </para>
/// <para>
/// <b>Register it last.</b> Filter order is registration order and first-registered is outermost, so registering this
/// after every other filter — <c>AddSecondLevelEventRetry()</c> above all — leaves it innermost, wrapping only the
/// dispatch core. That matters because the rehydrated envelope carries the full body again: a retry filter sitting
/// <i>inside</i> this one re-publishes that envelope, which puts the whole body back through the send path and writes a
/// second blob on every retry hop. Outside it, those filters keep re-publishing the small reference envelope, and so
/// does <see cref="ReceiveContext.DeadLetterAsync"/> — a dead-letter record holds the pointer, not the payload, which is
/// what lets a redrive still work and what keeps the dead-letter store small.
/// </para>
/// <para>
/// That ordering depends on the reference surviving a re-publish, which is what
/// <see cref="MessageHeaders.IsAdapterOwned"/> buys: an adapter now drops only the keys it re-derives from the envelope,
/// not the whole <see cref="MessageHeaders.ReservedPrefix"/> namespace, so <see cref="ClaimCheckHeaders.Reference"/>
/// rides a retry, delay or redrive hop intact — as do
/// <see cref="Reliability.SecondLevelRetryHeaders.Tier"/> and <see cref="Reliability.DeadLetterHeaders.RedriveCount"/>.
/// The send half relies on the same fact in reverse: seeing the reference on an outgoing envelope is how
/// <see cref="ClaimCheckOffloader"/> knows not to offload a pointer.
/// </para>
/// <para>
/// A body that cannot be read back throws <see cref="ClaimCheckPayloadException"/> <i>before</i> the chain continues, so
/// the resilience pipeline — which lives inside the core this wraps — never runs and the message goes straight to the
/// pipeline's dead-letter path with the exception's message as its reason. An application running delayed or
/// second-level retry can spend that budget on it first; add
/// <c>AddEventFaultClassification(r =&gt; r.DeadLetterOn&lt;ClaimCheckPayloadException&gt;())</c> to cut it short.
/// </para>
/// </remarks>
internal sealed partial class ClaimCheckRehydrateConsumeFilter(
    ClaimCheckPayloadStore store,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ILogger<ClaimCheckRehydrateConsumeFilter> logger) : IConsumeFilter
{
    public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var envelope = context.Envelope;

        // The whole cost of the feature on a normal message: one lookup for a header that is not there.
        if (!ClaimCheckHeaders.TryReadReference(envelope, out var path))
        {
            await next(context, cancellationToken);
            return;
        }

        var bodyType = ResolveBodyType(envelope);
        var payload = await store.ReadAsync(path, envelope.MessageId, cancellationToken);

        object body;
        try
        {
            body = serializer.Deserialize(payload, bodyType)
                ?? throw new ClaimCheckPayloadException($"Claim-checked body '{path}' for message '{envelope.MessageId}' deserialized to null.");
        }
        catch (Exception exception) when (exception is not (ClaimCheckPayloadException or OperationCanceledException))
        {
            // A serializer swap between producer and consumer, or a corrupt blob. Naming the claim check keeps the
            // dead-letter reason pointing at the payload rather than at an opaque decoder error.
            throw new ClaimCheckPayloadException(
                $"Claim-checked body '{path}' for message '{envelope.MessageId}' could not be deserialized as {bodyType.Name}.",
                exception);
        }

        LogRehydrated(envelope.MessageId, payload.LongLength, path);

        // Only the body and its type change. Settlement stays on the transport's own context, so acknowledgement and
        // dead-lettering still describe the message that actually arrived.
        await next(new RehydratedReceiveContext(context, envelope with { Body = body, BodyType = bodyType }), cancellationToken);
    }

    // The envelope's own type is right whenever the wire kept carrying it (which is what preserves routing), so the
    // header is only consulted as a fallback — and a header naming a type this process cannot resolve is ignored rather
    // than fatal, because the envelope already holds a resolved type to deserialize into.
    private Type ResolveBodyType(EventEnvelope envelope)
    {
        if (envelope.Headers.TryGetValue(ClaimCheckHeaders.BodyType, out var token)
            && !string.IsNullOrWhiteSpace(token)
            && typeResolver.ResolveType(token) is { } resolved)
        {
            return resolved;
        }

        return envelope.BodyType;
    }

    [LoggerMessage(EventId = 6082, Level = LogLevel.Debug, Message = "Rehydrated the {SizeBytes}-byte body of message {MessageId} from claim check {Path}")]
    private partial void LogRehydrated(string messageId, long sizeBytes, string path);

    /// <summary>The received message with its body put back — settlement delegates to the transport's own context.</summary>
    private sealed class RehydratedReceiveContext(ReceiveContext inner, EventEnvelope rehydrated) : ReceiveContext
    {
        public override EventEnvelope Envelope => rehydrated;

        public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
            => inner.AcknowledgeAsync(cancellationToken);

        public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
            => inner.DeadLetterAsync(reason, exception, cancellationToken);
    }
}

/// <summary>
/// Deletes claim-checked blobs once they age past <see cref="ClaimCheckOptions.Retention"/>. The only thing that ever
/// deletes one — consuming a message does not, because the same blob still has to serve the other subscribers of a
/// fan-out, the next retry, and any later redrive out of the dead-letter store.
/// </summary>
internal sealed partial class ClaimCheckRetentionSweeper(
    ClaimCheckPayloadStore store,
    IOptions<ClaimCheckOptions> options,
    TimeProvider timeProvider,
    ILogger<ClaimCheckRetentionSweeper> logger) : BackgroundService
{
    // Bounds the first pass over a store that has been accumulating without a sweep; the remainder goes next pass.
    private const int MaxDeletesPerSweep = 1_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options.Value;
        if (!config.Enabled || !config.SweepEnabled)
        {
            LogSweepDisabled();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await store.SweepAsync(timeProvider.GetUtcNow() - config.Retention, MaxDeletesPerSweep, stoppingToken);
                if (deleted > 0)
                    LogSwept(deleted, config.Retention);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A sweep that fails must not take the host down: the blobs it did not reach are storage cost, not a
                // correctness problem, and the next pass tries again.
                LogSweepFailed(exception);
            }

            try
            {
                await Task.Delay(config.SweepInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(EventId = 6083, Level = LogLevel.Debug, Message = "Claim-check retention sweep is disabled; offloaded bodies are expected to expire under the blob store's own lifecycle rules")]
    private partial void LogSweepDisabled();

    [LoggerMessage(EventId = 6084, Level = LogLevel.Information, Message = "Swept {DeletedCount} claim-check bodies older than {Retention}")]
    private partial void LogSwept(int deletedCount, TimeSpan retention);

    [LoggerMessage(EventId = 6085, Level = LogLevel.Warning, Message = "Claim-check retention sweep failed; retrying at the next interval")]
    private partial void LogSweepFailed(Exception exception);
}

/// <summary>DI registration for the claim-check pattern.</summary>
public static class ClaimCheckServiceCollectionExtensions
{
    /// <summary>
    /// Hold bodies over a size threshold in blob storage and send a pointer instead, rehydrating them before dispatch,
    /// so a large payload stops being a publish that the broker rejects outright.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional threshold, retention and sweep settings; the call itself is the opt-in.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <remarks>
    /// <para>
    /// Requires an <see cref="IBlobStorage"/> registration (<c>AddLocalBlobStorage</c> or a cloud adapter). Order
    /// relative to the transport does not matter; order relative to <c>AddConsumeFilter&lt;T&gt;()</c> and
    /// <c>AddSecondLevelEventRetry()</c> does — call this <b>last</b>, so the rehydrate sits innermost and the retry
    /// filters keep re-publishing the small reference envelope rather than the rehydrated one.
    /// </para>
    /// <para>
    /// Roll it out on consumers before producers: a consumer without this registered has no rehydrator, and a
    /// claim-checked message reaches its handler with a body that was never fetched.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddLocalBlobStorage("/var/lib/app/blobs");
    /// services.AddRabbitMqEventBus(typeof(Program).Assembly);
    ///
    /// services.AddSecondLevelEventRetry();
    /// services.AddEventClaimCheck();                                  // 128 KiB threshold, 7-day retention
    /// services.AddEventClaimCheck(o =>
    /// {
    ///     o.ThresholdBytes = 64 * 1024;
    ///     o.Retention = TimeSpan.FromDays(30);                        // must outlast dead-letter triage
    ///     o.SweepEnabled = false;                                     // S3 lifecycle rules expire them instead
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddEventClaimCheck(this IServiceCollection services, Action<ClaimCheckOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Enabled here rather than in the type's default, so a bound configuration section that omits the key cannot
        // silently change what goes on the wire.
        services.AddOptions<ClaimCheckOptions>()
            .Configure(options =>
            {
                options.Enabled = true;
                configure?.Invoke(options);
            })
            .Validate(options => options.ThresholdBytes > 0, "ClaimCheckOptions.ThresholdBytes must be positive.")
            .Validate(options => options.MaxPayloadBytes >= options.ThresholdBytes, "ClaimCheckOptions.MaxPayloadBytes must be at least ThresholdBytes, or an offloaded body cannot be read back.")
            .Validate(options => options.MaxPayloadBytes <= Array.MaxLength, "ClaimCheckOptions.MaxPayloadBytes cannot exceed the largest single array; a body is rehydrated into one buffer.")
            .Validate(options => options.Retention > TimeSpan.Zero, "ClaimCheckOptions.Retention must be positive.")
            .Validate(options => options.SweepInterval > TimeSpan.Zero, "ClaimCheckOptions.SweepInterval must be positive.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.PathPrefix), "ClaimCheckOptions.PathPrefix must name a blob path prefix.");

        // The wire body of an offloaded message IS a ClaimCheckReference, so the receiving adapter has to resolve that
        // token before any filter runs. Registered under a stable name rather than left to the resolver's
        // assembly-qualified-name fallback, whose version segment moves on every SDK push and would strand a message
        // sent by a producer running a different build.
        GetOrAddMessageTypeRegistry(services).Register(typeof(ClaimCheckReference), ClaimCheckReference.TypeToken);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ClaimCheckPayloadStore>();
        services.TryAddSingleton<ClaimCheckOffloader>();
        services.AddSingleton<IConsumeFilter, ClaimCheckRehydrateConsumeFilter>();
        services.AddHostedService<ClaimCheckRetentionSweeper>();
        return services;
    }

    // The transport registration looks the registry up the same way, by singleton INSTANCE, so seeding it here makes
    // this call order-independent relative to AddRabbitMqEventBus and friends: whichever runs first creates the one
    // instance, and the other finds it rather than replacing it with an empty second registry.
    private static MessageTypeRegistry GetOrAddMessageTypeRegistry(IServiceCollection services)
    {
        foreach (var descriptor in services)
            if (descriptor.ServiceType == typeof(MessageTypeRegistry) && descriptor.ImplementationInstance is MessageTypeRegistry existing)
                return existing;

        var registry = new MessageTypeRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
