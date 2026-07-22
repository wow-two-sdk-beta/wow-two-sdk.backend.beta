using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>Options for the Redis Streams event-bus adapter.</summary>
public sealed class RedisStreamsOptions
{
    /// <summary>StackExchange.Redis configuration string. Default <c>localhost:6379</c>.</summary>
    public string Configuration { get; set; } = "localhost:6379";

    /// <summary>Redis database index. Default <c>-1</c> (the connection's configured default). Redis Cluster only serves database 0.</summary>
    public int Database { get; set; } = -1;

    /// <summary>Stream key events are added to / read from. Default <c>wt.events</c>.</summary>
    public string Stream { get; set; } = "wt.events";

    /// <summary>
    /// Consumer group. Every instance of one service joins the same group so they compete for entries
    /// (<c>XREADGROUP</c> hands each entry to exactly one member) rather than each receiving everything. Default
    /// <c>wt-consumers</c>.
    /// </summary>
    public string ConsumerGroup { get; set; } = "wt-consumers";

    /// <summary>
    /// This instance's consumer name within <see cref="ConsumerGroup"/> — the owner of its pending-entries list (PEL).
    /// Null derives <c>{machine}-{pid}</c>, which is unique per process and stable for its lifetime.
    /// </summary>
    /// <remarks>
    /// Two instances sharing a name share a PEL, so one may claim an entry the other is still processing. A process
    /// that dies leaves its PEL behind under its old name; that is not a leak of messages — the claim path recovers
    /// them — but the dead consumer itself lingers in <c>XINFO CONSUMERS</c> until an operator runs
    /// <c>XGROUP DELCONSUMER</c>. Run that only against a consumer whose PEL is empty: it discards pending entries
    /// outright rather than returning them to the group.
    /// </remarks>
    public string? ConsumerName { get; set; }

    /// <summary>Dead-letter stream — Redis has no native dead-letter facility, so exhausted/poison entries are re-added here (emulated DLQ). Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterStream { get; set; } = "wt.events.dlq";

    /// <summary>
    /// Cap on entries kept in a stream, applied on every <c>XADD</c> (<c>MAXLEN</c>). Default 100 000; null keeps the
    /// stream untrimmed and therefore unbounded — a stream is an append-only log and acknowledging an entry does not
    /// remove it, so without a cap the key grows until Redis evicts or OOMs.
    /// </summary>
    /// <remarks>
    /// Trimming can evict an entry that is still pending in some consumer's PEL. The claim path detects that case — the
    /// id is pending, <c>XCLAIM</c> returns nothing for it, <em>and</em> the entry is no longer in the stream — and
    /// acknowledges the id to purge the phantom PEL row, so a trimmed-away entry costs a lost message rather than a
    /// permanently stuck one. Size the cap above the worst case backlog anyway: the message really is gone.
    /// </remarks>
    public int? MaxLength { get; set; } = 100_000;

    /// <summary>
    /// Trim approximately (<c>MAXLEN ~ N</c>) rather than exactly. Default true — approximate trimming stops at a macro
    /// node boundary and is amortized O(1), where exact trimming walks the entries it removes. The stream then sits at
    /// or slightly above <see cref="MaxLength"/>, never below.
    /// </summary>
    public bool UseApproximateMaxLength { get; set; } = true;

    /// <summary>
    /// Cap on the dead-letter stream. Default null (never trimmed): a dead letter is the only remaining copy of a
    /// message the system failed to process, and dropping it to bound a key trades a visible backlog for silent data
    /// loss. Draining the DLQ is an operator action; set this only where that trade is understood.
    /// </summary>
    public int? DeadLetterMaxLength { get; set; }

    /// <summary>Maximum entries fetched per stream per read, and per claim sweep (<c>COUNT</c>). Default 32.</summary>
    public int BatchSize { get; set; } = 32;

    /// <summary>
    /// How long the consume loop waits after an empty read before polling again. Default 250 ms.
    /// </summary>
    /// <remarks>
    /// The loop polls rather than blocking because StackExchange.Redis multiplexes every command over one connection
    /// and therefore exposes no <c>BLOCK</c> argument on <c>XREADGROUP</c> — a blocking read would stall every other
    /// command sharing the multiplexer. This interval is consequently the adapter's idle latency floor; it applies only
    /// when a read came back empty, so a busy stream is drained back-to-back with no delay.
    /// </remarks>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>How often the consume loop sweeps the pending-entries list for stale entries to claim. Default 30 s. The first sweep runs at startup, which is when orphaned entries are most likely.</summary>
    public TimeSpan ClaimInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long an entry must sit unacknowledged in a consumer's PEL before another consumer may claim it. Default
    /// 5 minutes.
    /// </summary>
    /// <remarks>
    /// This is the one value that decides whether recovery duplicates live work. Idle time is measured from the last
    /// delivery, not from the last sign of life, so a handler still running after this long looks exactly like a dead
    /// one and its entry is claimed underneath it. Keep it above the slowest handler's worst case; the SDK inbox
    /// dedupes the resulting replay, but the handler runs twice concurrently before that helps.
    /// </remarks>
    public TimeSpan MinIdleTimeBeforeClaim { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Deliveries an entry may accumulate in the PEL before the claim path dead-letters it instead of redelivering.
    /// Default 5. Prevents a message that kills its consumer from being claimed forever.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Route each message to the stream <see cref="ITopologyProvider"/> resolves from it — the message type's stable
    /// token for a publish, <see cref="EventEnvelope.Destination"/> for an explicit
    /// <see cref="IEventBus.SendAsync{TEvent}"/> — instead of adding everything to <see cref="Stream"/>. Routed streams
    /// are nested under <see cref="Stream"/> (<c>wt.events.order-placed</c>). Default false, which keeps an existing
    /// deployment on its single stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opt-in because it changes which key a message lands on, and a consumer still on the old build reads
    /// <see cref="Stream"/> alone. Migrate consumers first: a consumer with this on reads <see cref="Stream"/>
    /// <em>as well as</em> every routed stream, so it keeps receiving from producers that have not been switched yet.
    /// </para>
    /// <para>
    /// On Redis Cluster one <c>XREADGROUP</c> over several streams requires every key to hash to the same slot, or the
    /// server answers CROSSSLOT. Give <see cref="Stream"/> a hash tag (<c>{wt.events}</c>) so the root and everything
    /// nested under it share a slot; the adapter neither adds nor assumes one.
    /// </para>
    /// </remarks>
    public bool RouteByDestination { get; set; }
}

/// <summary>Well-known stream-entry field names carrying SDK envelope metadata. A stream entry is a flat field/value map, so each header is its own field alongside the body.</summary>
/// <remarks>
/// Every cross-broker name is an alias of <see cref="MessageHeaders"/>, never a literal: the wire contract is one
/// constant per key for all adapters, so a rename cannot leave Redis spelling the old string while Kafka and NATS
/// spell the new one. Only the keys Redis alone needs — the body field and the dead-letter provenance fields — are
/// declared here.
/// </remarks>
internal static class RedisStreamsFields
{
    /// <summary>
    /// The serialized body. Reserved, and stripped on send by <see cref="IsAdapterOwned"/> — not by the reserved
    /// prefix, which the send path stopped filtering on so that headers SDK features stamp survive the wire.
    /// </summary>
    public const string Body = MessageHeaders.ReservedPrefix + "body";

    /// <summary>Carries the event's stable type token so the consumer can resolve the CLR type.</summary>
    public const string EventType = MessageHeaders.EventType;

    /// <summary>Carries the serializer content type, so the consumer selects the matching deserializer.</summary>
    public const string ContentType = MessageHeaders.ContentType;

    /// <summary>Carries <see cref="EventEnvelope.MessageId"/> — Redis assigns its own entry id, so the SDK's identity needs a field of its own.</summary>
    public const string MessageId = MessageHeaders.MessageId;

    /// <summary>Carries the envelope's ordering / partition key so the consumer can preserve per-key ordering.</summary>
    public const string PartitionKey = MessageHeaders.PartitionKey;

    /// <summary>Carries <see cref="EventEnvelope.CorrelationId"/>. Redis has no correlation property, so it rides a field.</summary>
    public const string CorrelationId = MessageHeaders.CorrelationId;

    /// <summary>Why the entry was dead-lettered, stamped on the copy written to the dead-letter stream.</summary>
    public const string DeadLetterReason = MessageHeaders.DeadLetterReason;

    /// <summary>Type name of the terminal exception, stamped alongside <see cref="DeadLetterReason"/>.</summary>
    public const string DeadLetterExceptionType = MessageHeaders.DeadLetterExceptionType;

    /// <summary>Stream the entry died on. The DLQ is one key for every routed stream, so without this a dead letter cannot be traced back to its source.</summary>
    public const string DeadLetterSourceStream = MessageHeaders.ReservedPrefix + "dl-source-stream";

    /// <summary>PEL delivery count at the moment of death — how many attempts the message actually consumed.</summary>
    public const string DeadLetterDeliveryCount = MessageHeaders.ReservedPrefix + "dl-delivery-count";

    /// <summary>
    /// True when this adapter re-derives <paramref name="name"/> on every write, so a caller-supplied copy must be
    /// dropped instead of riding the wire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Redis-scoped superset of <see cref="MessageHeaders.IsAdapterOwned"/>. That predicate is the cross-broker set
    /// and deliberately cannot name a key only one broker has, so the fields declared here — the body and the
    /// dead-letter provenance — need an owner on this side of the seam.
    /// </para>
    /// <para>
    /// A stream entry is a field <em>list</em>, not a map, so an unstripped copy is not overwritten by the genuine
    /// value appended after it: both ride, and which one a reader sees is a question of scan direction.
    /// </para>
    /// </remarks>
    /// <param name="name">The field name.</param>
    public static bool IsAdapterOwned(string name)
        => MessageHeaders.IsAdapterOwned(name) || name is Body || IsDeathField(name);

    /// <summary>
    /// True for the provenance fields stamped only on the dead-letter copy. Excludes <see cref="Body"/> on purpose —
    /// the dead-letter copy has to keep the body it died on, so this is the narrower of the two strips.
    /// </summary>
    /// <param name="name">The field name.</param>
    public static bool IsDeathField(string name)
        => name is DeadLetterReason
            or DeadLetterExceptionType
            or DeadLetterSourceStream
            or DeadLetterDeliveryCount;
}

/// <summary>Maps a topology routing key onto a stream key nested under the adapter's configured root stream.</summary>
/// <remarks>
/// <para>
/// Redis keys are binary safe, so nesting is a naming convention rather than a requirement — but it is what lets an
/// operator find every stream of one deployment (<c>SCAN MATCH wt.events*</c>) and what makes a single cluster hash tag
/// on the root cover the whole set.
/// </para>
/// <para>
/// Both halves of the adapter call this on the same keys, so the stream the send path adds to is by construction one
/// the consume loop reads.
/// </para>
/// </remarks>
internal static class RedisStreamName
{
    /// <summary>Ceiling on a generated key. Redis itself allows 512 MB keys; this keeps a routing key from producing something no human can read in <c>SCAN</c> output.</summary>
    private const int MaxLength = 512;

    /// <summary>The stream key for <paramref name="routingKey"/> under <paramref name="root"/>.</summary>
    /// <param name="root">The configured root stream (<see cref="RedisStreamsOptions.Stream"/>).</param>
    /// <param name="routingKey">A topology routing key, or an address.</param>
    public static string Route(string root, string? routingKey)
    {
        var token = Sanitize(routingKey);
        if (token is null)
            return root;

        // Already rooted, so it is not prefixed again. This is what makes the mapping idempotent: a consumed envelope
        // carries its routed stream as Destination, and a re-add of it (delayed retry, DLQ replay) has to land back on
        // that same key rather than on wt.events.wt.events.order-placed, which nothing reads.
        if (IsRootedAt(token, root))
            return token;

        var routed = string.Concat(root, ".", token);
        return routed.Length <= MaxLength ? routed : routed[..MaxLength];
    }

    /// <summary>True when <paramref name="key"/> is <paramref name="root"/> itself or sits beneath it.</summary>
    /// <param name="key">The key to test.</param>
    /// <param name="root">The root stream key.</param>
    public static bool IsRootedAt(string key, string root)
        => string.Equals(key, root, StringComparison.Ordinal)
            || (key.Length > root.Length && key.StartsWith(root, StringComparison.Ordinal) && key[root.Length] == '.');

    /// <summary>
    /// A key-safe token, or null when nothing addressable survives. Every key Redis accepts is legal on the wire, so
    /// this guards operability rather than validity: <c>{</c> and <c>}</c> above all, which Redis Cluster reads as a
    /// hash tag and which would silently move one routed stream to a different slot than its siblings — the shape that
    /// breaks a multi-stream read. Glob metacharacters go too, so <c>SCAN MATCH</c> over the set behaves.
    /// </summary>
    private static string? Sanitize(string? routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
            return null;

        var builder = new StringBuilder(Math.Min(routingKey.Length, MaxLength));
        foreach (var character in routingKey)
        {
            if (builder.Length == MaxLength)
                break;

            // Deliberately an ASCII test, not char.IsLetterOrDigit: the latter admits non-ASCII letters, which are
            // valid keys but undiagnosable in redis-cli output. Everything else collapses to '-', so two distinct
            // routing keys stay two distinct streams.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_';
            builder.Append(legal ? character : '-');
        }

        var key = builder.ToString().Trim('.');
        return key.Length == 0 ? null : key;
    }
}

/// <summary>Stream-entry wire format — envelope to field/value pairs on send, and back on receive.</summary>
internal static class RedisStreamsWireFormat
{
    /// <summary>The field/value pairs for one outgoing envelope.</summary>
    public static NameValueEntry[] BuildEntry(EventEnvelope envelope, byte[] body, string typeToken, string contentType)
    {
        // Caller headers first, minus the fields this adapter re-derives below, then the control fields. A received
        // envelope's Headers carry the wt-* fields of THAT message, so forwarding them on a re-publish would otherwise
        // stamp the previous message's type token onto the new body and misroute it on the consumer — and, for a
        // replayed dead letter, re-assert death provenance on a message that is alive.
        var fields = new List<NameValueEntry>(envelope.Headers.Count + 8);
        foreach (var (key, value) in envelope.Headers)
            if (!RedisStreamsFields.IsAdapterOwned(key))
                fields.Add(new NameValueEntry(key, value));

        fields.Add(new NameValueEntry(RedisStreamsFields.EventType, typeToken));
        fields.Add(new NameValueEntry(RedisStreamsFields.ContentType, contentType));
        fields.Add(new NameValueEntry(RedisStreamsFields.MessageId, envelope.MessageId));

        if (!string.IsNullOrEmpty(envelope.PartitionKey))
            fields.Add(new NameValueEntry(RedisStreamsFields.PartitionKey, envelope.PartitionKey));

        // Redis has no reply-address, correlation or conversation property, so all three ride the SDK's reserved
        // fields. Stamped only when set, so ordinary one-way traffic carries exactly the fields it did before.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            fields.Add(new NameValueEntry(MessageHeaders.ReplyTo, envelope.ReplyTo));
        if (!string.IsNullOrEmpty(envelope.CorrelationId))
            fields.Add(new NameValueEntry(RedisStreamsFields.CorrelationId, envelope.CorrelationId));
        if (!string.IsNullOrEmpty(envelope.ConversationId))
            fields.Add(new NameValueEntry(MessageHeaders.ConversationId, envelope.ConversationId));

        // Last, so an operator running XRANGE sees the metadata before a wall of serialized body.
        fields.Add(new NameValueEntry(RedisStreamsFields.Body, body));
        return [.. fields];
    }

    /// <summary>Every field of a received entry except the body, as the envelope's headers.</summary>
    public static Dictionary<string, string> DecodeHeaders(StreamEntry entry)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        if (entry.Values is not { } values)
            return headers;

        foreach (var field in values)
        {
            var name = field.Name.ToString();
            if (name.Length == 0 || string.Equals(name, RedisStreamsFields.Body, StringComparison.Ordinal))
                continue;

            headers[name] = field.Value.ToString();
        }

        return headers;
    }

    /// <summary>The serialized body of a received entry, or null when the entry carries none (not one of ours, or truncated).</summary>
    public static byte[]? ReadBody(StreamEntry entry)
    {
        if (entry.Values is not { } values)
            return null;

        // Scanned backwards rather than read through entry[Body], because the indexer returns the FIRST field of that
        // name and an entry is a list: a duplicate shadows the real value instead of replacing it. The send path strips
        // a caller's copy, so this defends the entries it did not author — a hand-rolled XADD, a foreign producer — for
        // which last is the right pick either way, since BuildEntry appends the body last and DecodeHeaders is
        // last-wins. A null-valued field is skipped so a truncated trailing copy cannot mask a body that is present.
        for (var index = values.Length - 1; index >= 0; index--)
        {
            var field = values[index];
            if (!field.Value.IsNull && string.Equals(field.Name.ToString(), RedisStreamsFields.Body, StringComparison.Ordinal))
                return (byte[]?)field.Value;
        }

        return null;
    }

    /// <summary>The dead-letter entry for a poison message — every original field plus death fields for triage.</summary>
    public static NameValueEntry[] BuildDeadLetterEntry(StreamEntry original, string sourceStream, int deliveryCount, string reason, Exception? exception)
    {
        var fields = new List<NameValueEntry>((original.Values?.Length ?? 0) + 4);
        if (original.Values is { } values)
        {
            foreach (var field in values)
            {
                // A message that was dead-lettered, replayed and died again would otherwise carry two of each death
                // field, and a lookup by name returns the first — which would be the stale reason.
                var name = field.Name.ToString();
                if (RedisStreamsFields.IsDeathField(name))
                    continue;

                fields.Add(field);
            }
        }

        fields.Add(new NameValueEntry(RedisStreamsFields.DeadLetterReason, reason));
        fields.Add(new NameValueEntry(RedisStreamsFields.DeadLetterSourceStream, sourceStream));
        fields.Add(new NameValueEntry(RedisStreamsFields.DeadLetterDeliveryCount, deliveryCount.ToString(CultureInfo.InvariantCulture)));
        if (exception is not null)
            fields.Add(new NameValueEntry(RedisStreamsFields.DeadLetterExceptionType, exception.GetType().FullName ?? exception.GetType().Name));

        return [.. fields];
    }
}

/// <summary>
/// The adapter's connection to Redis, shared by the send and receive halves. One multiplexer, because that is what
/// StackExchange.Redis is built for — it is thread-safe, pipelines concurrent callers, and reconnects on its own — and
/// because nothing here blocks a connection the way a <c>BLOCK</c>-ing read would.
/// </summary>
internal sealed class RedisStreamsConnection(IOptions<RedisStreamsOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private ConnectionMultiplexer? _multiplexer; // concrete: CA1859 — this field is only ever assigned ConnectionMultiplexer.ConnectAsync

    /// <summary>The configured database, connecting on first use.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async ValueTask<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken)
    {
        // Connect lazily rather than in the constructor: DI graph construction must not do I/O, and a Redis that is
        // not up yet should fail the first send or the consume loop, not the host's service-provider build.
        if (_multiplexer is { } connected)
            return connected.GetDatabase(options.Value.Database);

        await _connectGate.WaitAsync(cancellationToken);
        try
        {
            _multiplexer ??= await ConnectionMultiplexer.ConnectAsync(options.Value.Configuration);
            return _multiplexer.GetDatabase(options.Value.Database);
        }
        finally
        {
            _connectGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Connection first, gate last. The reverse order hands a caller racing shutdown an ObjectDisposedException out
        // of WaitAsync — a fault, where closing the multiplexer underneath it produces the ordinary connection error
        // the send path already handles.
        if (_multiplexer is { } multiplexer)
        {
            _multiplexer = null;
            await multiplexer.CloseAsync();
            multiplexer.Dispose();
        }

        _connectGate.Dispose();
    }
}

/// <summary>Idempotent consumer-group provisioning.</summary>
internal static class RedisStreamsTopology
{
    /// <summary>Redis' "the group already exists" error, which is the normal answer on every start after the first.</summary>
    private const string GroupExistsPrefix = "BUSYGROUP";

    /// <summary>Redis' "no such key or consumer group" error, raised by a read after the key was dropped underneath the loop.</summary>
    public const string GroupMissingPrefix = "NOGROUP";

    /// <summary>Create the consumer group on every stream this process reads. Safe to repeat and safe to race.</summary>
    /// <param name="database">The Redis database.</param>
    /// <param name="streams">The stream keys to provision.</param>
    /// <param name="consumerGroup">The consumer group name.</param>
    public static async ValueTask EnsureGroupsAsync(IDatabase database, IReadOnlyList<string> streams, string consumerGroup)
    {
        foreach (var stream in streams)
        {
            try
            {
                // createStream: the group has to exist before the first message does, or the consume loop starts with
                // a NOGROUP against a key nobody has written yet. MKSTREAM creates an empty stream for exactly that.
                // Position NewMessages ("$"): a group joining an existing stream starts at its tail, so standing up a
                // new service does not replay whatever history the stream happens to still hold.
                await database.StreamCreateConsumerGroupAsync(stream, consumerGroup, StreamPosition.NewMessages, createStream: true);
            }
            catch (RedisServerException exception) when (exception.Message.StartsWith(GroupExistsPrefix, StringComparison.Ordinal))
            {
                // Group already provisioned, by an earlier run or by another instance racing this one.
            }
        }
    }
}

/// <summary>
/// Redis Streams <see cref="ISendTransport"/> — <c>XADD</c>s a JSON-serialized event as a flat field/value entry. The
/// stream key comes from <see cref="ITopologyProvider"/> once <see cref="RedisStreamsOptions.RouteByDestination"/> is
/// on: the message type's stable token for a publish, the caller's address for an explicit send.
/// </summary>
internal sealed class RedisStreamsSendTransport(
    IOptions<RedisStreamsOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ITopologyProvider topology,
    RedisStreamsConnection connection) : ISendTransport
{
    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var database = await connection.GetDatabaseAsync(cancellationToken);
        var body = envelope.ToWireBody(serializer);

        // WireBodyType, not BodyType: a send-path transformation that substituted the wire bytes decodes as a different
        // shape, and this token is what the receiver deserializes into. Stream resolution below stays on BodyType.
        var fields = RedisStreamsWireFormat.BuildEntry(envelope, body, typeResolver.ToTypeToken(envelope.WireBodyType), serializer.ContentType);

        // messageId null lets Redis assign the entry id, which is what keeps the stream's ordering invariant: ids are
        // monotonic and an XADD with an id at or below the tail is rejected outright. The SDK's own MessageId rides a
        // field instead, so the two identities never fight.
        await database.StreamAddAsync(
            ResolveStream(envelope),
            fields,
            messageId: null,
            maxLength: options.Value.MaxLength,
            useApproximateMaxLength: options.Value.UseApproximateMaxLength);
    }

    /// <summary>
    /// The stream this envelope is added to. Off (the default) every message rides
    /// <see cref="RedisStreamsOptions.Stream"/>. On, the topology decides — and it is the topology, not the raw
    /// destination, for the same reason the RabbitMQ adapter routes on the resolved key: a publish has to land on the
    /// type's own stream, which is what a consumer of that type reads, while an explicit send has to land on the
    /// addressed endpoint's stream so it reaches that endpoint alone.
    /// </summary>
    private string ResolveStream(EventEnvelope envelope)
        => options.Value.RouteByDestination
            ? RedisStreamName.Route(options.Value.Stream, topology.ResolveRoutingKey(envelope))
            : options.Value.Stream;
}

/// <summary>
/// Redis Streams <see cref="ReceiveContext"/>. Settlement is an ordinary command on a thread-safe multiplexer, so
/// (unlike Kafka) the message is settled here: acknowledge is <c>XACK</c>, which removes the entry from this
/// consumer's pending-entries list; dead-letter <c>XADD</c>s the original to the dead-letter stream (emulated DLQ)
/// then acks, so nothing claims the poison entry again.
/// </summary>
internal sealed class RedisStreamsReceiveContext(
    EventEnvelope envelope,
    IDatabase database,
    RedisStreamsOptions options,
    string sourceStream,
    StreamEntry entry) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override async ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
    {
        // XACK only clears the PEL — the entry stays in the stream, because a stream is a log and another consumer
        // group may not have read it yet. MAXLEN on XADD is what bounds the key; deleting acked entries here would
        // punch holes in every other group's view of it.
        await database.StreamAcknowledgeAsync(sourceStream, options.ConsumerGroup, entry.Id);
    }

    public override async ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        await database.StreamAddAsync(
            options.DeadLetterStream,
            RedisStreamsWireFormat.BuildDeadLetterEntry(entry, sourceStream, envelope.DeliveryCount, reason, exception),
            messageId: null,
            maxLength: options.DeadLetterMaxLength,
            useApproximateMaxLength: options.UseApproximateMaxLength);

        // Ack only after the DLQ write lands. The reverse order loses the message outright if the second command
        // fails; this order can at worst duplicate it into the DLQ on a later claim, which is recoverable.
        await database.StreamAcknowledgeAsync(sourceStream, options.ConsumerGroup, entry.Id);
    }
}

/// <summary>
/// Redis Streams <see cref="IReceiveTransport"/> — provisions the consumer group, then drives a poll loop that
/// interleaves reading undelivered entries (<c>XREADGROUP &gt;</c>) with claiming entries stranded in a dead
/// consumer's pending-entries list (<c>XPENDING</c> + <c>XCLAIM</c>). The streams it reads are the mirror image of the
/// send path's stream resolution: every routing key <see cref="ITopologyProvider"/> declares for this process, mapped
/// through the same <see cref="RedisStreamName"/>.
/// </summary>
internal sealed partial class RedisStreamsReceiveTransport(
    IOptions<RedisStreamsOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ITopologyProvider topology,
    RedisStreamsConnection connection,
    ILogger<RedisStreamsReceiveTransport> logger,
    IReplyAddressProvider? replyAddresses = null,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    /// <summary>First pause after a transient Redis fault. Short, because the common cause — a failover or a reconnect — clears in about this long and the loop should be reading again as soon as it does.</summary>
    private static readonly TimeSpan FaultBackoff = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Ceiling on the doubled pause. A fault that keeps recurring is an outage rather than a blip, and this is what
    /// stops the loop from retrying it thousands of times an hour: consumption still resumes on its own, at the cost of
    /// one log line and up to this much latency once Redis comes back.
    /// </summary>
    private static readonly TimeSpan MaxFaultBackoff = TimeSpan.FromSeconds(30);

    private readonly string _consumerName = options.Value.ConsumerName is { Length: > 0 } configured
        ? configured
        : string.Create(CultureInfo.InvariantCulture, $"{Environment.MachineName}-{Environment.ProcessId}");

    private CancellationTokenSource? _cts;
    private Task? _loop;
    private long? _lastClaimTimestamp;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        var database = await connection.GetDatabaseAsync(cancellationToken);
        var streams = ResolveConsumeStreams();
        LogConsumeStreams(string.Join(", ", streams), _consumerName);

        await RedisStreamsTopology.EnsureGroupsAsync(database, streams, options.Value.ConsumerGroup);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = Task.Run(() => ConsumeLoopAsync(database, streams, onMessage, _cts.Token), CancellationToken.None);
        _loop = loop;

        // Awaited, not fired and forgotten, matching the ASB and RabbitMQ adapters: this method IS the hosted service's
        // ExecuteAsync body. Returning here completed ExecuteAsync at startup, which collapsed the documented
        // stop→drain→release order — base.StopAsync had nothing left to await, so pump.DrainAsync ran while this loop
        // was still admitting messages.
        //
        // This is also what carries the loop's terminal fault (see ConsumeLoopAsync — transient faults are absorbed,
        // the rest re-throw) into ExecuteAsync, where the host's BackgroundServiceExceptionBehavior acts on it. Under
        // the .NET default, StopHost, the process goes down. Deliberate: the terminal class is the one a retry cannot
        // change — WRONGTYPE, NOAUTH, a bug here — so consumption is over for the life of this process, and a service
        // that stays up while consuming nothing fails silently where a restarting one fails loudly. An app that would
        // rather keep serving sets BackgroundServiceExceptionBehavior.Ignore; swallowing it here offers no such choice.
        await loop;
    }

    /// <summary>
    /// Every stream this process reads. <see cref="RedisStreamsOptions.Stream"/> is always in the set — with routing
    /// off it is the only one, and with routing on it is what keeps a publisher that has not been switched yet
    /// reachable, which is what makes a consumers-first rollout possible.
    /// </summary>
    /// <remarks>
    /// The routed part is <see cref="EndpointTopology.RoutingKeys"/>, the same list the RabbitMQ adapter binds to its
    /// queue: one key per consumed message type (so a publish of that type arrives) plus the endpoint's own name (so an
    /// explicit send addressed to this endpoint — a reply, above all — arrives). Redis has no exchange to bind through,
    /// so the key set becomes the read set instead.
    /// </remarks>
    private List<string> ResolveConsumeStreams()
    {
        var opt = options.Value;
        var streams = new List<string> { opt.Stream };
        if (!opt.RouteByDestination)
            return streams;

        foreach (var endpoint in topology.ConsumeEndpoints)
            foreach (var routingKey in endpoint.RoutingKeys)
                AddStream(streams, RedisStreamName.Route(opt.Stream, routingKey), opt.DeadLetterStream);

        // A configured per-instance reply address is not part of the topology — it is this process's alone — so the
        // topology cannot know about it. Without this the request client would stamp a ReplyTo nothing reads and every
        // request would time out, which is the whole point of addressing a reply.
        if (replyAddresses is not null)
            AddStream(streams, RedisStreamName.Route(opt.Stream, replyAddresses.ReplyAddress), opt.DeadLetterStream);

        return streams;

        static void AddStream(List<string> streams, string stream, string deadLetterStream)
        {
            // Never read the dead-letter stream. The default DLQ key sits under the same root as the routed streams,
            // so a routing key that happens to resolve onto it would put poison messages straight back into the
            // pipeline that dead-lettered them — an unbounded loop, at DLQ-write cost per lap.
            if (stream.Length == 0
                || string.Equals(stream, deadLetterStream, StringComparison.Ordinal)
                || streams.Contains(stream, StringComparer.Ordinal))
            {
                return;
            }

            streams.Add(stream);
        }
    }

    /// <summary>
    /// Poll until cancelled. The loop has to outlive a Redis fault: a timeout or a dropped connection out of a read is
    /// an outage the server recovers from, and faulting the task over one would end consumption for the life of the
    /// process — silently, since nothing observes this task until <see cref="StopAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same shape as the RabbitMQ supervisor: catch, log, pause, go round again. The pause doubles from
    /// <see cref="FaultBackoff"/> to <see cref="MaxFaultBackoff"/> and resets on the first clean round, so a one-second
    /// failover costs a one-second gap while a long outage settles into a retry every 30 s rather than a spin.
    /// </para>
    /// <para>
    /// Only transient faults are absorbed — see <see cref="IsTransientRedisFault"/>. Anything else (a bug here, a
    /// misconfigured key, a command Redis rejects outright) still ends the loop, because retrying it forever would
    /// convert a loud failure into a quiet one that merely logs. It is logged on the way out either way, which is what
    /// was missing: the fault used to reach nobody until a shutdown that may never come.
    /// </para>
    /// </remarks>
    private async Task ConsumeLoopAsync(
        IDatabase database,
        List<string> streams,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        var positions = streams.Select(stream => new StreamPosition(stream, StreamPosition.NewMessages)).ToArray();
        var backoff = FaultBackoff;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var handled = 0;

                    if (IsClaimDue())
                    {
                        _lastClaimTimestamp = Stopwatch.GetTimestamp();
                        handled += await ClaimStaleAsync(database, streams, onMessage, cancellationToken);
                    }

                    handled += await ReadNewAsync(database, positions, onMessage, cancellationToken);

                    // Only idle when the round produced nothing: a busy stream is drained batch after batch with no
                    // delay, and the poll interval costs latency exclusively when there is nothing to lose it on.
                    if (handled == 0)
                        await Task.Delay(options.Value.PollInterval, cancellationToken);

                    // A round that completed is the end of the fault, so the next one starts from the short pause again.
                    backoff = FaultBackoff;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception) when (IsTransientRedisFault(exception))
                {
                    LogTransientFault(backoff.TotalSeconds, exception);
                    await DelayAsync(backoff, cancellationToken);
                    backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxFaultBackoff.Ticks));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // shutdown
        }
        catch (Exception exception) when (cancellationToken.IsCancellationRequested)
        {
            // Torn down mid-command — the multiplexer closing underneath an in-flight read is the ordinary shape of
            // shutdown, not something to fail the host's stop path over.
            LogConsumeLoopTornDown(exception);
        }
        catch (Exception exception)
        {
            // Terminal. Consumption is over for this process, so it is logged here rather than left for StopAsync to
            // rethrow — that is the one place it surfaced before, and only if the host ever stopped.
            LogConsumeLoopFaulted(exception);
            throw;
        }
    }

    /// <summary>
    /// True for a fault the loop should wait out rather than die on: the command timed out on the multiplexer, the
    /// connection is down or reconnecting, or the server answered with an error that means "not now".
    /// </summary>
    /// <remarks>
    /// Every other <see cref="RedisServerException"/> — <c>WRONGTYPE</c>, a syntax error, <c>NOAUTH</c> — reports
    /// something a retry cannot change, and is deliberately left to end the loop. <c>NOGROUP</c> is absent because it
    /// is already recovered where it is raised, by re-creating the group and reading again on the next round.
    /// </remarks>
    /// <param name="exception">The fault raised by a read, a claim or a settle.</param>
    private static bool IsTransientRedisFault(Exception exception) => exception switch
    {
        RedisTimeoutException => true,
        RedisConnectionException => true,
        RedisServerException server => IsTransientServerError(server.Message),
        _ => false,
    };

    /// <summary>
    /// True for the server error replies that clear on their own: a node loading its dataset, a failover in progress
    /// (<c>MASTERDOWN</c> / <c>READONLY</c>), a cluster resharding or without a covering master, a write refused for
    /// want of replicas, or persistence temporarily failing (<c>MISCONF</c>).
    /// </summary>
    /// <remarks><c>BUSY</c> is not in the set on purpose: <c>BUSYGROUP</c> shares the prefix, and that one is provisioning noise the topology already handles rather than anything to retry.</remarks>
    /// <param name="message">The server's error reply.</param>
    private static bool IsTransientServerError(string message)
        => message.StartsWith("LOADING", StringComparison.Ordinal)
            || message.StartsWith("MASTERDOWN", StringComparison.Ordinal)
            || message.StartsWith("CLUSTERDOWN", StringComparison.Ordinal)
            || message.StartsWith("TRYAGAIN", StringComparison.Ordinal)
            || message.StartsWith("READONLY", StringComparison.Ordinal)
            || message.StartsWith("NOREPLICAS", StringComparison.Ordinal)
            || message.StartsWith("MISCONF", StringComparison.Ordinal);

    /// <summary>Pause that treats cancellation as an ordinary wake-up, so a backoff cannot throw out of a loop that is already shutting down.</summary>
    /// <param name="delay">How long to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>Due at startup (an orphaned PEL is most likely right after a restart) and every <see cref="RedisStreamsOptions.ClaimInterval"/> thereafter.</summary>
    private bool IsClaimDue()
        => _lastClaimTimestamp is not { } last || Stopwatch.GetElapsedTime(last) >= options.Value.ClaimInterval;

    /// <summary>Read entries never delivered to anyone in the group, across every stream in one round trip.</summary>
    private async ValueTask<int> ReadNewAsync(
        IDatabase database,
        StreamPosition[] positions,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        RedisStream[] read;
        try
        {
            // StreamPosition.NewMessages resolves to ">" for a group read: entries never handed to any consumer in the
            // group. That is what makes instances compete instead of each receiving everything, and it is also why a
            // fresh delivery is always attempt 1 — anything redelivered comes back through the claim path, not here.
            read = await database.StreamReadGroupAsync(positions, options.Value.ConsumerGroup, _consumerName, countPerStream: options.Value.BatchSize);
        }
        catch (RedisServerException exception) when (exception.Message.StartsWith(RedisStreamsTopology.GroupMissingPrefix, StringComparison.Ordinal))
        {
            // The key or the group went away underneath us (FLUSHDB, a DEL, a failover to an empty replica). Rebuild
            // and let the next iteration read; throwing here would kill the loop for something recoverable.
            LogGroupMissing();
            await RedisStreamsTopology.EnsureGroupsAsync(database, [.. positions.Select(position => position.Key.ToString())], options.Value.ConsumerGroup);
            return 0;
        }

        var handled = 0;
        foreach (var stream in read)
        {
            if (stream.Entries is not { Length: > 0 } entries)
                continue;

            var streamKey = stream.Key.ToString();
            foreach (var entry in entries)
            {
                if (cancellationToken.IsCancellationRequested)
                    return handled;

                await HandleAsync(database, streamKey, entry, deliveryCount: 1, onMessage, cancellationToken);
                handled++;
            }
        }

        return handled;
    }

    /// <summary>
    /// Recover entries stranded in the pending-entries list of a consumer that stopped acking — the failure this
    /// adapter exists to survive. Without it, a message delivered to an instance that then crashed stays pending
    /// forever: <c>XREADGROUP &gt;</c> never returns it again, and no other consumer can see it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>XPENDING</c> before <c>XCLAIM</c>, rather than <c>XAUTOCLAIM</c> in one call, for two reasons. It reports each
    /// entry's true delivery count, which is the honest source for <see cref="EventEnvelope.DeliveryCount"/> and the
    /// only way to stop claiming a message that has already killed several consumers. And it needs only Redis 5.0,
    /// where <c>XAUTOCLAIM</c> needs 6.2.
    /// </para>
    /// <para>
    /// The idle time is passed to <c>XCLAIM</c> as well as filtered on, which is what makes a concurrent sweep safe:
    /// a claim by another instance resets the entry's idle clock to zero, so the losing instance's <c>XCLAIM</c> then
    /// declines it rather than handing the same message to two consumers.
    /// </para>
    /// </remarks>
    private async ValueTask<int> ClaimStaleAsync(
        IDatabase database,
        List<string> streams,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        var options0 = options.Value;
        var minIdleMs = (long)options0.MinIdleTimeBeforeClaim.TotalMilliseconds;
        var handled = 0;

        foreach (var stream in streams)
        {
            if (cancellationToken.IsCancellationRequested)
                return handled;

            // RedisValue.Null for the consumer means "every consumer in the group", which includes this one — an entry
            // this process was delivered and never settled (a hung handler, a crash between dispatch and ack) is
            // exactly as stranded as another instance's, and is recovered on the same path.
            var pending = await database.StreamPendingMessagesAsync(stream, options0.ConsumerGroup, options0.BatchSize, RedisValue.Null);
            if (pending is not { Length: > 0 })
                continue;

            var stale = pending.Where(message => message.IdleTimeInMilliseconds >= minIdleMs).ToArray();
            if (stale.Length == 0)
                continue;

            var deliveryCounts = new Dictionary<RedisValue, int>(stale.Length);
            foreach (var message in stale)
                deliveryCounts[message.MessageId] = message.DeliveryCount;

            var ids = stale.Select(message => message.MessageId).ToArray();
            var claimed = await database.StreamClaimAsync(stream, options0.ConsumerGroup, _consumerName, minIdleMs, ids);

            var recovered = new HashSet<RedisValue>();
            foreach (var entry in claimed)
            {
                if (entry.IsNull || entry.Values is not { Length: > 0 })
                    continue;

                recovered.Add(entry.Id);

                if (cancellationToken.IsCancellationRequested)
                    return handled;

                // The claim itself is a delivery, so the count Redis reported before it is one short of this attempt.
                var deliveryCount = deliveryCounts.GetValueOrDefault(entry.Id) + 1;
                if (deliveryCount > options0.MaxDeliveryAttempts)
                {
                    LogPoisonEntry(entry.Id.ToString(), stream, deliveryCount);
                    await DeadLetterRawAsync(database, stream, entry, deliveryCount, "delivery-attempts-exhausted", exception: null);
                    handled++;
                    continue;
                }

                LogClaimed(entry.Id.ToString(), stream, deliveryCount);
                await HandleAsync(database, stream, entry, deliveryCount, onMessage, cancellationToken);
                handled++;
            }

            await PurgePhantomsAsync(database, stream, ids, recovered);
        }

        return handled;
    }

    /// <summary>
    /// Acknowledge pending ids whose entry is gone from the stream — trimmed out while still pending, so the PEL row
    /// points at data that no longer exists. Left alone it is claimed, skipped and re-counted on every sweep forever,
    /// and it holds <c>XPENDING</c>'s bounded window against entries that are genuinely recoverable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>XCLAIM</c> returning nothing for an id is not by itself evidence of a phantom. The other cause is another
    /// instance claiming it first: that resets the entry's idle clock, which is what makes this instance's claim
    /// decline. The two have to be told apart before anything is acked, because <c>XACK</c> is scoped to the
    /// <em>group</em> and not to the consumer — it deletes the pending row of whichever consumer holds it, from any
    /// caller, and reports it acknowledged. Acking the entry the winner just claimed would erase the only record that
    /// it is being processed; if that consumer then died mid-handler the message would be unrecoverable rather than
    /// redelivered, and nothing would say so.
    /// </para>
    /// <para>
    /// So each unclaimed id is looked up in the stream and acked only when the entry is absent. That is safe whoever
    /// owns the row: with the data gone no consumer can process it, so the row is dead weight rather than someone's
    /// work in flight. The check cannot go stale between the lookup and the ack either — stream ids are monotonic and
    /// <c>XADD</c> rejects any id at or below the tail, so a trimmed id can never come back.
    /// </para>
    /// </remarks>
    private async ValueTask PurgePhantomsAsync(IDatabase database, string stream, RedisValue[] requested, HashSet<RedisValue> recovered)
    {
        var unclaimed = requested.Where(id => !recovered.Contains(id)).ToArray();
        if (unclaimed.Length == 0)
            return;

        // One XRANGE per unclaimed id, all issued before the first await so the multiplexer pipelines them instead of
        // paying a round trip each. The set is capped by BatchSize and is empty on a healthy sweep, so the common path
        // costs nothing.
        var lookups = new Task<StreamEntry[]>[unclaimed.Length];
        for (var index = 0; index < unclaimed.Length; index++)
            lookups[index] = database.StreamRangeAsync(stream, unclaimed[index], unclaimed[index], count: 1);

        var phantoms = new List<RedisValue>(unclaimed.Length);
        for (var index = 0; index < unclaimed.Length; index++)
        {
            // Still in the stream → a live entry another consumer owns. Leave its pending row alone; the owner acks it,
            // or it goes idle again and this sweep reclaims it on the honest path.
            if (await lookups[index] is { Length: > 0 })
                continue;

            phantoms.Add(unclaimed[index]);
        }

        if (phantoms.Count == 0)
            return;

        var acknowledged = await database.StreamAcknowledgeAsync(stream, options.Value.ConsumerGroup, [.. phantoms]);
        if (acknowledged > 0)
            LogPhantomsPurged(acknowledged, stream);
    }

    private async ValueTask HandleAsync(
        IDatabase database,
        string stream,
        StreamEntry entry,
        int deliveryCount,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        var envelope = TryReconstruct(stream, entry, deliveryCount);
        if (envelope is null)
        {
            LogUnparseable();
            // Don't silently drop — copy the raw entry to the dead-letter stream, then ack so it stops being claimed.
            await DeadLetterRawAsync(database, stream, entry, deliveryCount, "unparseable", exception: null);
            return;
        }

        var context = new RedisStreamsReceiveContext(envelope, database, options.Value, stream, entry);
        try
        {
            // The pipeline settles via the context (ctx.Acknowledge on success / ctx.DeadLetter on exhaustion).
            await onMessage(context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Unsettled → the entry stays in this consumer's PEL and the claim path recovers it once it goes idle
            // (deduped by the inbox). This is the crash safety-net, not the retry path: in-process retries already ran.
            LogProcessingError(exception);
        }
    }

    /// <summary>
    /// Dead-letter an entry that never became an envelope (unparseable payload, exhausted claim), so there is no
    /// <see cref="ReceiveContext"/> to settle through. Reads this instance's own options — the DLQ key and the consumer
    /// group are per-registration, and a process hosting two buses must not settle one's entries against the other's.
    /// </summary>
    private async ValueTask DeadLetterRawAsync(IDatabase database, string stream, StreamEntry entry, int deliveryCount, string reason, Exception? exception)
    {
        var opt = options.Value;

        await database.StreamAddAsync(
            opt.DeadLetterStream,
            RedisStreamsWireFormat.BuildDeadLetterEntry(entry, stream, deliveryCount, reason, exception),
            messageId: null,
            maxLength: opt.DeadLetterMaxLength,
            useApproximateMaxLength: opt.UseApproximateMaxLength);

        // Ack only after the DLQ write lands, matching RedisStreamsReceiveContext: the reverse order loses the entry
        // outright if the second command fails, where this order can at worst duplicate it into the DLQ on a later claim.
        await database.StreamAcknowledgeAsync(stream, opt.ConsumerGroup, entry.Id);
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is { } cts)
            await cts.CancelAsync();

        if (_loop is { } loop)
        {
            try
            {
                await loop.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // stop timed out or loop cancelled — proceed
            }
            catch (Exception)
            {
                // The loop faulted terminally. StartAsync awaits the same task, so the fault has already been logged by
                // ConsumeLoopAsync and surfaced through ExecuteAsync; re-throwing it here would only abort the release
                // below on the way down.
            }
        }

        await DisposeAsync();
    }

    /// <summary>
    /// Release the consume loop's cancellation source. Idempotent, and reached twice on an ordinary shutdown — once
    /// through <see cref="StopAsync"/>, once when the container disposes this singleton. The second path is the one
    /// that matters: a host that faults before <c>StopAsync</c> runs would otherwise leave the loop polling a
    /// multiplexer the container is closing underneath it.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // The multiplexer is owned by RedisStreamsConnection (a DI singleton shared with the send transport), so it is
        // deliberately not closed here: the send path may still be flushing an outbox during shutdown.
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }
    }

    private EventEnvelope? TryReconstruct(string stream, StreamEntry entry, int deliveryCount)
    {
        var headers = RedisStreamsWireFormat.DecodeHeaders(entry);
        if (!headers.TryGetValue(RedisStreamsFields.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        if (RedisStreamsWireFormat.ReadBody(entry) is not { } data)
            return null;

        // Decoded by whoever encoded it. Selection reads "declared or nothing", never the "application/json" the
        // envelope below falls back to: a producer that stamped no content type is one whose format we do not know, and
        // guessing JSON for it would route a legacy body to the wrong deserializer wherever the default is not JSON.
        var body = SerializerFor(ReadOptional(headers, RedisStreamsFields.ContentType)).Deserialize(data, eventType);
        if (body is null)
            return null;

        return new EventEnvelope
        {
            MessageId = headers.TryGetValue(RedisStreamsFields.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = stream,
            // 1-based, matching the NATS adapter's NumDelivered: 1 is the first delivery, not zero prior attempts.
            // Unlike Kafka's header-carried counter this is the broker's own number — the PEL counts deliveries, so a
            // message redelivered by a claim reports the truth even across a producer that never saw it.
            DeliveryCount = deliveryCount,
            ContentType = headers.TryGetValue(RedisStreamsFields.ContentType, out var contentType) ? contentType : "application/json",
            PartitionKey = ReadOptional(headers, RedisStreamsFields.PartitionKey),
            ReplyTo = ReadOptional(headers, MessageHeaders.ReplyTo),
            CorrelationId = ReadOptional(headers, RedisStreamsFields.CorrelationId),
            ConversationId = ReadOptional(headers, MessageHeaders.ConversationId),
            Headers = headers,
        };
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer whenever no registry is wired, which is what keeps a single-serializer container behaving exactly as it did.</summary>
    private IMessageSerializer SerializerFor(string? contentType) => serializerRegistry?.Resolve(contentType) ?? serializer;

    [LoggerMessage(EventId = 6601, Level = LogLevel.Warning, Message = "Discarding unparseable Redis stream entry")]
    private partial void LogUnparseable();

    [LoggerMessage(EventId = 6602, Level = LogLevel.Error, Message = "Redis stream entry processing failed; not acknowledged (stays pending, recovered by claim)")]
    private partial void LogProcessingError(Exception exception);

    [LoggerMessage(EventId = 6603, Level = LogLevel.Information, Message = "Reading Redis streams {Streams} as consumer {Consumer}")]
    private partial void LogConsumeStreams(string streams, string consumer);

    [LoggerMessage(EventId = 6604, Level = LogLevel.Information, Message = "Claimed stale Redis stream entry {EntryId} on {Stream} (delivery {DeliveryCount})")]
    private partial void LogClaimed(string entryId, string stream, int deliveryCount);

    [LoggerMessage(EventId = 6605, Level = LogLevel.Warning, Message = "Redis stream entry {EntryId} on {Stream} exhausted its delivery attempts at {DeliveryCount}; dead-lettering")]
    private partial void LogPoisonEntry(string entryId, string stream, int deliveryCount);

    [LoggerMessage(EventId = 6606, Level = LogLevel.Warning, Message = "Purged {Count} pending Redis stream entries on {Stream} whose data was trimmed away")]
    private partial void LogPhantomsPurged(long count, string stream);

    [LoggerMessage(EventId = 6607, Level = LogLevel.Warning, Message = "Redis consumer group missing; re-creating and retrying")]
    private partial void LogGroupMissing();

    [LoggerMessage(EventId = 6608, Level = LogLevel.Warning, Message = "Redis consume loop hit a transient fault; retrying in {BackoffSeconds}s")]
    private partial void LogTransientFault(double backoffSeconds, Exception exception);

    [LoggerMessage(EventId = 6609, Level = LogLevel.Debug, Message = "Redis consume loop ended on a fault raised while shutting down")]
    private partial void LogConsumeLoopTornDown(Exception exception);

    [LoggerMessage(EventId = 6610, Level = LogLevel.Error, Message = "Redis consume loop faulted and will not restart; this process has stopped consuming")]
    private partial void LogConsumeLoopFaulted(Exception exception);
}

/// <summary>
/// Redis Streams capability flags. A stream is an ordered log with consumer groups over it, so the honest answers are
/// clustered at "no broker feature, the SDK emulates it": no dead-letter facility, no delay or scheduling, no priority,
/// no per-message TTL, no sessions, no producer transactions, no reply address. What it does have is a real
/// pending-entries list, which makes redelivery counting and stale-message recovery first-class rather than emulated,
/// and settlement that is safe from any thread.
/// </summary>
internal sealed class RedisStreamsCapabilities : ITransportCapabilities
{
    /// <summary>No dead-letter facility of any kind — the SDK emulates one by adding the entry to a separate stream and acking the original.</summary>
    public bool NativeDeadLetter => false;

    /// <summary>No delayed delivery: an <c>XADD</c>ed entry is immediately readable. Holding it back means a sorted set keyed by due time and a mover — an emulation, not a broker feature.</summary>
    public bool NativeDelay => false;

    /// <summary>
    /// No dedupe by message id. <c>XADD</c> with an explicit id rejects one at or below the stream's tail, but that
    /// enforces id monotonicity, not idempotency — it cannot recognize a re-send of an entry already deeper in the log.
    /// </summary>
    public bool NativeDedupe => false;

    /// <summary>A stream is a strictly ordered append-only log and entries are delivered to a group in id order. Ordering is per stream, and (as on every competing-consumer broker) it is the read that stays ordered, not the concurrent processing of what was read.</summary>
    public bool NativeOrdering => true;

    /// <summary>No per-message priority. Entries are ranked by id — that is, by insertion time — so nothing can overtake anything already in the stream.</summary>
    public bool NativePriority => false;

    /// <summary>
    /// Conditional, so reported false. <c>MAXLEN</c> and <c>MINID</c> bound a whole stream on one shared policy, which
    /// is the retention shape this flag explicitly excludes; there is no per-entry deadline, and a key-level
    /// <c>EXPIRE</c> would take the entire stream with it rather than one message.
    /// </summary>
    public bool NativeTimeToLive => false;

    /// <summary>No absolute-time enqueue. Entry ids are timestamps of when the entry was written, not of when it becomes readable.</summary>
    public bool NativeScheduling => false;

    /// <summary>
    /// No session or group affinity. A consumer owns individual entries through its pending-entries list, not a key
    /// range — two messages sharing a partition key land on whichever consumers <c>XREADGROUP</c> hands them to, and
    /// nothing locks a group of related messages to one consumer.
    /// </summary>
    public bool NativeSessions => false;

    /// <summary>
    /// <c>XADD</c> is a synchronous command that answers with the assigned entry id, so the send path learns the entry
    /// was accepted into the stream rather than merely written to a socket.
    /// </summary>
    /// <remarks>
    /// Acceptance is not fsync: Redis persists asynchronously (<c>appendfsync everysec</c> by default) and replicates
    /// asynchronously, so a master that dies within the window loses acknowledged entries, and a failover promotes a
    /// replica that never saw them. Closing that gap is <c>WAIT</c> / <c>appendfsync always</c>, which this adapter
    /// does not impose.
    /// </remarks>
    public bool NativePublisherConfirms => true;

    /// <summary>
    /// <c>MULTI</c>/<c>EXEC</c> gives atomic batch execution only — no idempotent producer, no dedupe across a retry,
    /// and no consume-transform-produce atomicity — which is precisely the shape this flag excludes.
    /// </summary>
    public bool NativeTransactions => false;

    /// <summary>No reply-address mechanism. Request-reply over streams means provisioning a reply stream and correlating by field, which is the SDK's work, not Redis'.</summary>
    public bool NativeRequestReply => false;

    /// <summary><c>XACK</c> is an ordinary command on a thread-safe multiplexer, so settlement is safe from a pump worker thread.</summary>
    public bool SettlesInContext => true;

    /// <summary>The consume loop is a poll over an async API with no thread ownership — nothing has to stay on the loop thread.</summary>
    public bool ThreadAffineConsume => false;
}

/// <summary>DI registration for the Redis Streams event-bus adapter.</summary>
public static class RedisStreamsServiceCollectionExtensions
{
    /// <summary>
    /// Register the Redis Streams transport behind the SDK event bus: send/receive transports (stream + consumer group
    /// + emulated dead-letter stream), the shared <see cref="IEventBus"/>, resilience defaults, topology, and the
    /// consumer hosted service. Scans the supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// <para>
    /// Set <see cref="RedisStreamsOptions.RouteByDestination"/> to route each message to its own stream; the topology
    /// derived from the handler scan is then what the consume loop reads. To change the endpoint shape, call
    /// <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> with its options before this — the
    /// two calls share one consumed-type set, and only the names this method back-fills from
    /// <see cref="RedisStreamsOptions"/> are left to it.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Redis options (configuration string, stream, consumer group, dead-letter stream, trimming, claim tuning).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddRedisStreamsEventBus(
        this IServiceCollection services,
        Action<RedisStreamsOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<RedisStreamsOptions>().Configure(configure);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan, never before: the consumed-type set that becomes the routed streams is read out of
        // the registered IEventHandler<> descriptors, and a type registered later gets no stream.
        services.AddMessageTopology();

        // The shared endpoint's name is this adapter's stream, so an explicit send addressed to this process (a reply)
        // resolves to the stream it already reads rather than to a nested one nothing reads. Null-coalesce rather than
        // assign — an explicit AddMessageTopology(...) still wins.
        services.AddOptions<TopologyOptions>().PostConfigure<IOptions<RedisStreamsOptions>>((topology, redis) =>
        {
            topology.SharedEndpointName ??= redis.Value.Stream;
            topology.SharedDeadLetterQueueName ??= redis.Value.DeadLetterStream;
        });

        services.TryAddSingleton<RedisStreamsConnection>();
        services.TryAddSingleton<ITransportCapabilities, RedisStreamsCapabilities>();
        services.TryAddSingleton<ISendTransport, RedisStreamsSendTransport>();
        services.TryAddSingleton<IReceiveTransport, RedisStreamsReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }
}
