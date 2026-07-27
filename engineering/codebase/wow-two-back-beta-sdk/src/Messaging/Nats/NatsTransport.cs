using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>Options for the NATS JetStream event-bus adapter.</summary>
public sealed class NatsOptions
{
    /// <summary>NATS server URL. Default <c>nats://localhost:4222</c>.</summary>
    public string Url { get; set; } = "nats://localhost:4222";

    /// <summary>JetStream stream that persists events (and dead-letters). Created if absent. Default <c>wt-events</c>.</summary>
    public string Stream { get; set; } = "wt-events";

    /// <summary>Subject events are published to / consumed from. Default <c>wt.events</c>.</summary>
    public string Subject { get; set; } = "wt.events";

    /// <summary>Durable consumer name — survives restarts and tracks acked offsets. Default <c>wt-consumers</c>.</summary>
    public string DurableConsumer { get; set; } = "wt-consumers";

    /// <summary>Dead-letter subject — JetStream has no native DLQ, so exhausted/poison messages are re-published here (emulated DLQ). Default <c>wt.events.dlq</c>.</summary>
    public string DeadLetterSubject { get; set; } = "wt.events.dlq";

    /// <summary>Max JetStream delivery attempts before the message is abandoned by the broker (crash safety-net; in-process retries are handled by the SDK resilience pipeline). Default 5.</summary>
    public int MaxDeliver { get; set; } = 5;

    /// <summary>
    /// Route each message to the subject <see cref="ITopologyProvider"/> resolves from it — the message type's stable
    /// token for a publish, <see cref="EventEnvelope.Destination"/> for an explicit
    /// <see cref="IEventBus.SendAsync{TEvent}"/> — instead of publishing everything to <see cref="Subject"/>. Routed
    /// subjects are nested under <see cref="Subject"/> (<c>wt.events.order-placed</c>). Default false, which keeps an
    /// existing deployment on its single subject.
    /// <para>
    /// Opt-in for three reasons. It changes which subject a message lands on, and a consumer still on the old build
    /// filters <see cref="Subject"/> alone — so migrate consumers first, since a consumer with this on filters
    /// <see cref="Subject"/> <em>as well as</em> every routed subject. It widens the stream's subject list with
    /// <c>{Subject}.&gt;</c>, which is a real change to a provisioned stream. And the durable consumer then uses
    /// JetStream's multi-subject filter, which needs nats-server 2.10 or newer.
    /// </para>
    /// </summary>
    public bool RouteByDestination { get; set; }
}

/// <summary>
/// Maps a topology routing key onto a NATS subject nested under the adapter's configured root subject.
/// </summary>
/// <remarks>
/// <para>
/// Nesting rather than using the key as a top-level subject is what lets the stream claim one wildcard
/// (<c>{root}.&gt;</c>) and thereby accept a send to any address, including an endpoint owned by another service. A
/// top-level scheme cannot: JetStream rejects a stream whose subjects overlap another stream's, so claiming
/// <c>Contracts.&gt;</c> would collide with any other stream in the account.
/// </para>
/// <para>
/// Both halves of the adapter call this on the same keys, so the subject the send path publishes to is by construction
/// one the durable consumer filters for.
/// </para>
/// </remarks>
internal static class NatsSubjectName
{
    /// <summary>The subject for <paramref name="routingKey"/> under <paramref name="root"/>.</summary>
    /// <param name="root">The configured root subject (<see cref="NatsOptions.Subject"/>).</param>
    /// <param name="routingKey">A topology routing key, or an address.</param>
    public static string Route(string root, string? routingKey)
    {
        var token = Sanitize(routingKey);
        if (token is null)
            return root;

        // Already rooted, so it is not prefixed again. This is what makes the mapping idempotent: a consumed envelope
        // carries its routed subject as Destination, and a re-publish of it (delayed retry, outbox replay) has to land
        // back on that same subject rather than on wt.events.wt.events.order-placed, which nothing filters.
        if (IsRootedAt(token, root))
            return token;

        return string.Concat(root, ".", token);
    }

    /// <summary>True when <paramref name="subject"/> is <paramref name="root"/> itself or sits beneath it.</summary>
    /// <param name="subject">The subject to test.</param>
    /// <param name="root">The root subject.</param>
    public static bool IsRootedAt(string subject, string root)
        => string.Equals(subject, root, StringComparison.Ordinal)
            || (subject.Length > root.Length && subject.StartsWith(root, StringComparison.Ordinal) && subject[root.Length] == '.');

    /// <summary>
    /// A legal subject token sequence, or null when nothing addressable survives. NATS separates tokens with <c>.</c>
    /// and reserves <c>*</c> and <c>&gt;</c> as wildcards — a key carrying either would silently widen the subscription
    /// — while an empty token (from <c>..</c>, or a leading <c>.</c>) is rejected outright by the server.
    /// </summary>
    private static string? Sanitize(string? routingKey)
    {
        if (string.IsNullOrEmpty(routingKey))
            return null;

        var builder = new StringBuilder(routingKey.Length);
        var atTokenStart = true; // true also at index 0, so a leading '.' cannot open an empty token
        foreach (var character in routingKey)
        {
            if (character is '.')
            {
                if (!atTokenStart)
                {
                    builder.Append('.');
                    atTokenStart = true;
                }

                continue;
            }

            // ASCII only, deliberately: char.IsLetterOrDigit admits characters that are legal in a subject but make it
            // undiagnosable in nats CLI output. Everything else collapses to '-', so two distinct keys stay distinct.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_';
            builder.Append(legal ? character : '-');
            atTokenStart = false;
        }

        var subject = builder.ToString().TrimEnd('.');
        return subject.Length == 0 ? null : subject;
    }
}

/// <summary>Shared JetStream envelope wire-format helpers (serialize body + headers; reconstruct on receive).</summary>
internal static class NatsWireFormat
{
    public static NatsHeaders BuildHeaders(EventEnvelope envelope, string typeToken, string contentType)
    {
        // Caller headers first, minus the reserved namespace, then the control headers — the adapter owns wt-*. A received
        // envelope's Headers carry the wt-* keys of THAT message, so forwarding them on a re-publish would otherwise stamp
        // the previous message's type token onto the new body and misroute it on the consumer.
        var headers = new NatsHeaders();
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaders.IsAdapterOwned(key))
                headers[key] = value;

        headers[MessageHeaders.EventType] = typeToken;
        headers[MessageHeaders.ContentType] = contentType;
        headers[MessageHeaders.MessageId] = envelope.MessageId;
        if (!string.IsNullOrEmpty(envelope.PartitionKey))
            headers[MessageHeaders.PartitionKey] = envelope.PartitionKey;

        // Core NATS routes a reply through the request's own subject field; a JetStream publish has no such field, so
        // the reply address, the correlation id and the conversation id ride the SDK's reserved headers. Stamped only
        // when set, so ordinary one-way traffic carries exactly the headers it did before.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            headers[MessageHeaders.ReplyTo] = envelope.ReplyTo;
        if (!string.IsNullOrEmpty(envelope.CorrelationId))
            headers[MessageHeaders.CorrelationId] = envelope.CorrelationId;
        if (!string.IsNullOrEmpty(envelope.ConversationId))
            headers[MessageHeaders.ConversationId] = envelope.ConversationId;

        return headers;
    }

    public static Dictionary<string, string> DecodeHeaders(NatsHeaders? headers)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
            return decoded;

        foreach (var key in headers.Keys)
            decoded[key] = headers[key].ToString();

        return decoded;
    }

    public static NatsHeaders BuildDeadLetterHeaders(NatsHeaders? original, string reason, Exception? exception)
    {
        var headers = new NatsHeaders();
        if (original is not null)
            foreach (var key in original.Keys)
                headers[key] = original[key];
        headers[MessageHeaders.DeadLetterReason] = reason;
        if (exception is not null)
            headers[MessageHeaders.DeadLetterExceptionType] = exception.GetType().FullName ?? exception.GetType().Name;
        return headers;
    }
}

/// <summary>
/// NATS JetStream <see cref="ISendTransport"/> — publishes JSON-serialized events to a stream subject. The subject
/// comes from <see cref="ITopologyProvider"/> once <see cref="NatsOptions.RouteByDestination"/> is on: the message
/// type's stable token for a publish, the caller's address for an explicit send. Ensures the stream exists on first send.
/// </summary>
internal sealed class NatsSendTransport(
    IOptions<NatsOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ITopologyProvider topology) : ISendTransport, IAsyncDisposable
{
    private readonly NatsConnection _connection = new(new NatsOpts { Url = options.Value.Url });
    private readonly SemaphoreSlim _provisionGate = new(1, 1);
    private NatsJSContext? _js;
    private bool _streamReady;

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var js = await EnsureStreamAsync(cancellationToken);
        var body = envelope.ToWireBody(serializer);

        // WireBodyType, not BodyType: a send-path transformation that substituted the wire bytes decodes as a different
        // shape, and this token is what the receiver deserializes into. Subject resolution below stays on BodyType.
        var headers = NatsWireFormat.BuildHeaders(envelope, typeResolver.ToTypeToken(envelope.WireBodyType), serializer.ContentType);

        await js.PublishAsync(ResolveSubject(envelope), body, headers: headers, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// The subject this envelope is published to. Off (the default) every message rides
    /// <see cref="NatsOptions.Subject"/>, exactly as before. On, the topology decides — and it is the topology, not the
    /// raw destination, for the same reason the RabbitMQ adapter routes on the resolved key: a publish has to land on
    /// the type's own subject, which is what a consumer of that type filters, while an explicit send has to land on the
    /// addressed endpoint's subject so it reaches that endpoint alone.
    /// </summary>
    private string ResolveSubject(EventEnvelope envelope)
        => options.Value.RouteByDestination
            ? NatsSubjectName.Route(options.Value.Subject, topology.ResolveRoutingKey(envelope))
            : options.Value.Subject;

    private async ValueTask<NatsJSContext> EnsureStreamAsync(CancellationToken cancellationToken)
    {
        _js ??= new NatsJSContext(_connection);
        if (_streamReady)
            return _js;

        await _provisionGate.WaitAsync(cancellationToken);
        try
        {
            if (!_streamReady)
            {
                await NatsTopology.EnsureStreamAsync(_js, options.Value, cancellationToken);
                _streamReady = true;
            }
        }
        finally
        {
            _provisionGate.Release();
        }

        return _js;
    }

    public async ValueTask DisposeAsync()
    {
        _provisionGate.Dispose();
        await _connection.DisposeAsync();
    }
}

/// <summary>Ensures the JetStream stream (and durable consumer) exist — idempotent; ignores "already exists".</summary>
internal static class NatsTopology
{
    /// <summary>JetStream's "stream not found" — the API answers a STREAM.INFO for an absent stream with a 404.</summary>
    private const int StreamNotFoundCode = 404;

    public static async ValueTask EnsureStreamAsync(NatsJSContext js, NatsOptions options, CancellationToken cancellationToken)
    {
        var subjects = StreamSubjects(options);

        // Unrouted: create-and-swallow, unchanged. An existing deployment's stream is never rewritten, so nothing about
        // its retention, replicas or storage can move because a process restarted.
        if (!options.RouteByDestination)
        {
            await TryCreateStreamAsync(js, options.Stream, subjects, cancellationToken);
            return;
        }

        // Routed: the stream has to claim the wildcard, and a stream created by an earlier build does not — JetStream
        // rejects a publish to a subject no stream claims, so every routed send would fail. Widen it in place.
        StreamConfig? existing;
        try
        {
            existing = (await js.GetStreamAsync(options.Stream, cancellationToken: cancellationToken)).Info.Config;
        }
        catch (NatsJSApiException ex) when (ex.Error.Code == StreamNotFoundCode)
        {
            existing = null;
        }

        if (existing is null)
        {
            await TryCreateStreamAsync(js, options.Stream, subjects, cancellationToken);
            return;
        }

        // Read-modify-write rather than CreateOrUpdateStreamAsync: that call posts the config it is handed to
        // STREAM.UPDATE, which replaces the whole thing — a freshly-built StreamConfig carries model defaults, so it
        // would silently reset an operator's retention, max-age and replica settings on an existing stream.
        var merged = MergeSubjects(existing.Subjects, subjects, options.Subject);
        if (merged is null)
            return;

        existing.Subjects = merged;
        await js.UpdateStreamAsync(existing, cancellationToken);
    }

    public static async ValueTask EnsureConsumerAsync(
        NatsJSContext js,
        NatsOptions options,
        IReadOnlyList<string> consumeSubjects,
        CancellationToken cancellationToken)
    {
        var config = new ConsumerConfig(options.DurableConsumer)
        {
            AckPolicy = ConsumerConfigAckPolicy.Explicit,
            MaxDeliver = options.MaxDeliver,
        };

        // Exactly one of the two filter forms may be set — the server rejects a consumer carrying both. Unrouted keeps
        // the single filter it always had; routed lists the subjects literally rather than filtering {Subject}.> ,
        // which would swallow the dead-letter subject that lives under the same root and re-deliver poison messages
        // forever.
        if (options.RouteByDestination)
            config.FilterSubjects = [.. consumeSubjects];
        else
            config.FilterSubject = options.Subject;

        await js.CreateOrUpdateConsumerAsync(options.Stream, config, cancellationToken);
    }

    /// <summary>The subjects the stream must claim: the root, its wildcard when routing, and the dead-letter subject unless the wildcard already covers it.</summary>
    private static List<string> StreamSubjects(NatsOptions options)
    {
        var subjects = new List<string>(3) { options.Subject };

        if (options.RouteByDestination)
            subjects.Add(options.Subject + ".>");

        // Listing a subject the wildcard already matches would leave the stream carrying two overlapping entries for
        // one subject space, which is exactly the shape the server validates against.
        if (!options.RouteByDestination || !NatsSubjectName.IsRootedAt(options.DeadLetterSubject, options.Subject))
            subjects.Add(options.DeadLetterSubject);

        return subjects;
    }

    /// <summary>The stream's subjects widened with <paramref name="required"/>, or null when it already covers them.</summary>
    private static List<string>? MergeSubjects(ICollection<string>? current, List<string> required, string root)
    {
        // Anything the new wildcard subsumes is dropped rather than carried, for the same non-overlap reason. Testing
        // `required` first is what keeps this idempotent: the wildcard and the root are themselves rooted, and dropping
        // them only to re-add them below would report a change on every start and re-post the config forever.
        var merged = new List<string>();
        var changed = false;
        if (current is not null)
        {
            foreach (var subject in current)
            {
                if (!required.Contains(subject, StringComparer.Ordinal) && NatsSubjectName.IsRootedAt(subject, root))
                {
                    changed = true;
                    continue;
                }

                merged.Add(subject);
            }
        }

        foreach (var subject in required)
        {
            if (merged.Contains(subject, StringComparer.Ordinal))
                continue;

            merged.Add(subject);
            changed = true;
        }

        return changed ? merged : null;
    }

    private static async ValueTask TryCreateStreamAsync(NatsJSContext js, string stream, ICollection<string> subjects, CancellationToken cancellationToken)
    {
        try
        {
            await js.CreateStreamAsync(new StreamConfig(stream, subjects), cancellationToken);
        }
        catch (NatsJSApiException)
        {
            // Stream already provisioned (name in use, or another instance won the race) — treat as ready.
        }
    }
}

/// <summary>
/// NATS JetStream <see cref="ReceiveContext"/>. NATS.Net is thread-safe, so (unlike Kafka) the message is settled here:
/// acknowledge acks the JetStream message; dead-letter re-publishes the original to the dead-letter subject (emulated DLQ)
/// then acks so the broker stops redelivering the poison message.
/// </summary>
internal sealed class NatsReceiveContext(
    EventEnvelope envelope,
    NatsJSContext js,
    string deadLetterSubject,
    NatsJSMsg<byte[]> message) : ReceiveContext
{
    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
        => message.AckAsync(cancellationToken: cancellationToken);

    public override async ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        var headers = NatsWireFormat.BuildDeadLetterHeaders(message.Headers, reason, exception);
        await js.PublishAsync(deadLetterSubject, message.Data ?? [], headers: headers, cancellationToken: cancellationToken);
        await message.AckAsync(cancellationToken: cancellationToken);
    }
}

/// <summary>
/// NATS JetStream <see cref="IReceiveTransport"/> — provisions the stream + durable consumer, then drives an async
/// consume loop into the pipeline. The consumer's subject filter is the mirror image of the send path's subject
/// resolution: every routing key <see cref="ITopologyProvider"/> declares for this process, mapped through the same
/// <see cref="NatsSubjectName"/>.
/// </summary>
internal sealed partial class NatsReceiveTransport(
    IOptions<NatsOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ITopologyProvider topology,
    ILogger<NatsReceiveTransport> logger,
    IReplyAddressProvider? replyAddresses = null,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    private readonly NatsConnection _connection = new(new NatsOpts { Url = options.Value.Url });
    private NatsJSContext? _js;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        _js = new NatsJSContext(_connection);

        var subjects = ResolveConsumeSubjects();
        LogConsumeSubjects(string.Join(", ", subjects));

        await NatsTopology.EnsureStreamAsync(_js, options.Value, cancellationToken);
        await NatsTopology.EnsureConsumerAsync(_js, options.Value, subjects, cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = Task.Run(() => ConsumeLoopAsync(onMessage, _cts.Token), CancellationToken.None);
        _loop = loop;

        // Awaited, not fired and forgotten, matching the ASB and RabbitMQ adapters: this method IS the hosted service's
        // ExecuteAsync body. Returning here completed ExecuteAsync at startup, which collapsed the documented
        // stop→drain→release order — base.StopAsync had nothing left to await, so pump.DrainAsync ran while this loop
        // was still admitting messages.
        //
        // A fault the loop could not absorb therefore now reaches ExecuteAsync, where the host's
        // BackgroundServiceExceptionBehavior (StopHost by default) acts on it. Deliberate: consumption is over for the
        // life of the process at that point, and a service that stays up while consuming nothing fails silently.
        await loop;
    }

    /// <summary>
    /// Every subject this process consumes. <see cref="NatsOptions.Subject"/> is always in the set — with routing off
    /// it is the only one, and with routing on it is what keeps a publisher that has not been switched yet reachable,
    /// which is what makes a consumers-first rollout possible.
    /// </summary>
    /// <remarks>
    /// The routed part is <see cref="EndpointTopology.RoutingKeys"/>, the same list the RabbitMQ adapter binds to its
    /// queue: one key per consumed message type (so a publish of that type arrives) plus the endpoint's own name (so an
    /// explicit send addressed to this endpoint — a reply, above all — arrives). JetStream has no exchange to bind
    /// through, so the key set becomes the durable consumer's subject filter instead. Anything the send path can
    /// resolve for a type this process handles, or for an address it owns, is therefore already here.
    /// </remarks>
    private List<string> ResolveConsumeSubjects()
    {
        var opt = options.Value;
        var subjects = new List<string> { opt.Subject };
        if (!opt.RouteByDestination)
            return subjects;

        foreach (var endpoint in topology.ConsumeEndpoints)
            foreach (var routingKey in endpoint.RoutingKeys)
                AddSubject(subjects, NatsSubjectName.Route(opt.Subject, routingKey));

        // A configured per-instance reply address is not part of the topology — it is this process's alone — so the
        // topology cannot know about it. Without this the request client would stamp a ReplyTo nothing filters and
        // every request would time out, which is the whole point of addressing a reply.
        if (replyAddresses is not null)
            AddSubject(subjects, NatsSubjectName.Route(opt.Subject, replyAddresses.ReplyAddress));

        return subjects;

        static void AddSubject(List<string> subjects, string subject)
        {
            if (subject.Length != 0 && !subjects.Contains(subject, StringComparer.Ordinal))
                subjects.Add(subject);
        }
    }

    private async Task ConsumeLoopAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        var consumer = await _js!.GetConsumerAsync(options.Value.Stream, options.Value.DurableConsumer, cancellationToken);
        try
        {
            await foreach (var message in consumer.ConsumeAsync<byte[]>(cancellationToken: cancellationToken))
            {
                var envelope = TryReconstruct(message);
                if (envelope is null)
                {
                    LogUnparseable();
                    // Don't silently drop — re-publish the raw message to the dead-letter subject, then ack.
                    var deadLetterHeaders = NatsWireFormat.BuildDeadLetterHeaders(message.Headers, "unparseable", null);
                    await _js!.PublishAsync(options.Value.DeadLetterSubject, message.Data ?? [], headers: deadLetterHeaders, cancellationToken: cancellationToken);
                    await message.AckAsync(cancellationToken: cancellationToken);
                    continue;
                }

                var context = new NatsReceiveContext(envelope, _js!, options.Value.DeadLetterSubject, message);
                try
                {
                    // The pipeline settles via the context (ctx.Acknowledge on success / ctx.DeadLetter on exhaustion).
                    await onMessage(context, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    LogProcessingError(ex); // unsettled → JetStream redelivers after AckWait (deduped by the inbox)
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
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
                // stop timed out or loop cancelled — proceed to dispose
            }
            catch (Exception)
            {
                // The loop faulted. StartAsync awaits the same task, so the fault has already surfaced through
                // ExecuteAsync; re-throwing it here would only abort the release below on the way down.
            }
        }

        await DisposeAsync();
    }

    private EventEnvelope? TryReconstruct(NatsJSMsg<byte[]> message)
    {
        var headers = NatsWireFormat.DecodeHeaders(message.Headers);
        if (!headers.TryGetValue(MessageHeaders.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        if (message.Data is not { } data)
            return null;

        // Decoded by whoever encoded it. Selection reads "declared or nothing", never the "application/json" the
        // envelope below falls back to: a producer that stamped no content type is one whose format we do not know, and
        // guessing JSON for it would route a legacy body to the wrong deserializer wherever the default is not JSON.
        var body = SerializerFor(ReadOptional(headers, MessageHeaders.ContentType)).Deserialize(data, eventType);
        if (body is null)
            return null;

        return new EventEnvelope
        {
            MessageId = headers.TryGetValue(MessageHeaders.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = message.Subject,
            DeliveryCount = (int)(message.Metadata?.NumDelivered ?? 1), // JetStream tracks redelivery natively
            ContentType = headers.TryGetValue(MessageHeaders.ContentType, out var contentType) ? contentType : "application/json",
            PartitionKey = headers.TryGetValue(MessageHeaders.PartitionKey, out var partitionKey) && !string.IsNullOrEmpty(partitionKey) ? partitionKey : null,
            ReplyTo = ReadOptional(headers, MessageHeaders.ReplyTo),
            CorrelationId = ReadOptional(headers, MessageHeaders.CorrelationId),
            ConversationId = ReadOptional(headers, MessageHeaders.ConversationId),
            Headers = headers,
        };
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer whenever no registry is wired, which is what keeps a single-serializer container behaving exactly as it did.</summary>
    private IMessageSerializer SerializerFor(string? contentType) => serializerRegistry?.Resolve(contentType) ?? serializer;

    public async ValueTask DisposeAsync()
    {
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        await _connection.DisposeAsync();
    }

    [LoggerMessage(EventId = 6501, Level = LogLevel.Warning, Message = "Discarding unparseable NATS message")]
    private partial void LogUnparseable();

    [LoggerMessage(EventId = 6502, Level = LogLevel.Error, Message = "NATS message processing failed; not acknowledged (will redeliver)")]
    private partial void LogProcessingError(Exception exception);

    [LoggerMessage(EventId = 6503, Level = LogLevel.Information, Message = "Consuming NATS subjects {Subjects}")]
    private partial void LogConsumeSubjects(string subjects);
}

/// <summary>
/// NATS JetStream capability flags — no native DLQ (SDK emulates via a dead-letter subject); per-subject ordering;
/// PubAck on publish and core request-reply; no per-message priority, sessions or producer transactions, and the newer
/// per-message TTL / scheduling features are stream opt-ins this adapter does not enable. Acks are per-message and
/// thread-safe, so settlement runs from a pump worker and the consume loop has no affinity.
/// </summary>
internal sealed class NatsCapabilities : ITransportCapabilities
{
    public bool NativeDeadLetter => false;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => true;

    /// <summary>
    /// No per-message priority. JetStream 2.11 priority groups rank pull-consumers competing for a stream (overflow /
    /// pinned-client policies), not messages within it — nothing lets one message overtake another.
    /// </summary>
    public bool NativePriority => false;

    /// <summary>
    /// Conditional, so reported false. A stream expires by its shared <c>MaxAge</c>; per-message TTL exists only as the
    /// <c>Nats-TTL</c> header on NATS Server 2.11+ AND only when the stream sets <c>AllowMsgTTL</c>. NatsTopology
    /// creates the stream without that flag, so a per-message TTL header would be ignored by the server.
    /// </summary>
    public bool NativeTimeToLive => false;

    /// <summary>
    /// Conditional, so reported false. Absolute-time delivery exists as the <c>Nats-Schedule</c> header on NATS Server
    /// 2.12+ AND only when the stream sets <c>AllowMsgSchedules</c> — an opt-in that cannot be reverted once enabled.
    /// NatsTopology does not set it, so a scheduled-delivery header would not be honoured.
    /// </summary>
    public bool NativeScheduling => false;

    /// <summary>No session or message-group affinity: a durable consumer is not pinned to a key, and JetStream has no group-scoped exclusive lock comparable to ASB sessions.</summary>
    public bool NativeSessions => false;

    /// <summary>JetStream publish returns a <c>PubAck</c> carrying the stream sequence — a real broker acknowledgement of persistence.</summary>
    public bool NativePublisherConfirms => true;

    /// <summary>No producer transactions or exactly-once semantics; publishes are acknowledged individually, with no atomic multi-subject commit.</summary>
    public bool NativeTransactions => false;

    /// <summary>Core NATS request-reply: the request carries an <c>_INBOX</c> reply subject the server routes back, with no reply queue to provision.</summary>
    /// <remarks>
    /// <c>IRequestClient</c> carries the reply address in <see cref="MessageHeaders.ReplyTo"/> rather than over an
    /// <c>_INBOX</c>, because this adapter's send path is a JetStream publish and an <c>_INBOX</c> subject is not in the
    /// stream's subject list — replying there would be rejected. Using the native mechanism means a core-NATS publish
    /// path alongside the JetStream one, which also gives up persistence for the reply: a follow-up, and a trade.
    /// </remarks>
    public bool NativeRequestReply => true;

    /// <summary>JetStream acks are per-message and safe to send from a pump worker thread.</summary>
    public bool SettlesInContext => true;

    /// <summary>The consume loop is an ordinary async enumeration — no thread affinity to preserve.</summary>
    public bool ThreadAffineConsume => false;
}

/// <summary>DI registration for the NATS JetStream event-bus adapter.</summary>
public static class NatsServiceCollectionExtensions
{
    /// <summary>
    /// Register the NATS JetStream transport behind the SDK event bus: send/receive transports (stream subject +
    /// emulated dead-letter subject), the shared <see cref="IEventBus"/>, resilience defaults, topology, and the
    /// consumer hosted service. Scans the supplied assemblies (or the caller's) for <see cref="IEventHandler{TEvent}"/>.
    /// <para>
    /// Set <see cref="NatsOptions.RouteByDestination"/> to route each message to its own subject; the topology derived
    /// from the handler scan is then what the durable consumer filters. To change the endpoint shape, call
    /// <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/> with its options before this — the
    /// two calls share one consumed-type set, and only the names this method back-fills from <see cref="NatsOptions"/>
    /// are left to it.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">NATS options (url, stream, subject, durable consumer, dead-letter subject).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddNatsEventBus(
        this IServiceCollection services,
        Action<NatsOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<NatsOptions>().Configure(configure);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan, never before: the consumed-type set that becomes the routed subjects is read out of
        // the registered IEventHandler<> descriptors, and a type registered later gets no subject.
        services.AddMessageTopology();

        // The shared endpoint's name is this adapter's root subject, so an explicit send addressed to this process (a
        // reply) resolves to the subject it already consumes rather than to a nested one nothing filters. Null-coalesce
        // rather than assign — an explicit AddMessageTopology(...) still wins.
        services.AddOptions<TopologyOptions>().PostConfigure<IOptions<NatsOptions>>((topology, nats) =>
        {
            topology.SharedEndpointName ??= nats.Value.Subject;
            topology.SharedDeadLetterQueueName ??= nats.Value.DeadLetterSubject;
        });

        services.TryAddSingleton<ITransportCapabilities, NatsCapabilities>();
        services.TryAddSingleton<ISendTransport, NatsSendTransport>();
        services.TryAddSingleton<IReceiveTransport, NatsReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }
}
