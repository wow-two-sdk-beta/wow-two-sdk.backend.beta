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
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>
/// Redis Streams <see cref="IReceiveTransport"/> — provisions the consumer group, then drives a poll loop that
/// interleaves reading undelivered entries (<c>XREADGROUP &gt;</c>) with claiming entries stranded in a dead
/// consumer's pending-entries list (<c>XPENDING</c> + <c>XCLAIM</c>). The streams it reads are the mirror image of the
/// send path's stream resolution: every routing key <see cref="ITopologyService"/> declares for this process, mapped
/// through the same <see cref="RedisStreamNameMapper"/>.
/// </summary>
internal sealed partial class RedisStreamsReceiveTransport(
    IOptions<RedisStreamsOptions> options,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ITopologyService topology,
    RedisStreamsConnection connection,
    ILogger<RedisStreamsReceiveTransport> logger,
    IReplyAddressService? replyAddresses = null,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    private readonly RedisStreamsTopologyBroker _topology = new();
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

        await _topology.EnsureGroupsAsync(database, streams, options.Value.ConsumerGroup);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loop = Task.Run(() => ConsumeLoopAsync(database, streams, onMessage, _cts.Token), CancellationToken.None);
        _loop = loop;

        // ExecuteAsync's body — a terminal fault stops the host unless BackgroundServiceExceptionBehavior is Ignore.
        await loop;
    }

    /// <summary>
    /// Every stream this process reads. <see cref="RedisStreamsOptions.Stream"/> is always in the set — with routing
    /// off it is the only one, and with routing on it is what keeps a publisher that has not been switched yet
    /// reachable, which is what makes a consumers-first rollout possible.
    /// </summary>
    /// <remarks>
    ///   - the routed part is <see cref="EndpointTopology.RoutingKeys"/> — one key per consumed message type
    ///   - the endpoint's own name is read too, so an explicit send addressed here (a reply) arrives
    ///   - Redis has no exchange to bind through, so the key set becomes the read set instead
    /// </remarks>
    private List<string> ResolveConsumeStreams()
    {
        var opt = options.Value;
        var streams = new List<string> { opt.Stream };
        if (!opt.RouteByDestination)
            return streams;

        foreach (var endpoint in topology.ConsumeEndpoints)
            foreach (var routingKey in endpoint.RoutingKeys)
                AddStream(streams, RedisStreamNameMapper.Route(opt.Stream, routingKey), opt.DeadLetterStream);

        // A per-instance reply address is this process's alone, so add it here or every request times out.
        if (replyAddresses is not null)
            AddStream(streams, RedisStreamNameMapper.Route(opt.Stream, replyAddresses.ReplyAddress), opt.DeadLetterStream);

        return streams;

        static void AddStream(List<string> streams, string stream, string deadLetterStream)
        {
            // Never read the dead-letter stream — a poison message would loop straight back into the pipeline.
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
    ///   - the pause doubles from <see cref="FaultBackoff"/> to <see cref="MaxFaultBackoff"/>, resetting on a clean round
    ///   - only a transient fault is absorbed — see <see cref="IsTransientRedisFault"/>
    ///   - anything else ends the loop, logged on the way out, rather than retrying a failure a retry cannot change
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

                    // Idle only when the round produced nothing, so a busy stream drains batch after batch.
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
            // Torn down mid-command — the multiplexer closing under an in-flight read is ordinary shutdown.
            LogConsumeLoopTornDown(exception);
        }
        catch (Exception exception)
        {
            // Terminal — consumption is over for this process, so log it here.
            LogConsumeLoopFaulted(exception);
            throw;
        }
    }

    /// <summary>
    /// True for a fault the loop should wait out rather than die on: the command timed out on the multiplexer, the
    /// connection is down or reconnecting, or the server answered with an error that means "not now".
    /// </summary>
    /// <remarks>
    ///   - every other <see cref="RedisServerException"/> — <c>WRONGTYPE</c>, a syntax error, <c>NOAUTH</c> — ends the loop
    ///   - <c>NOGROUP</c> is absent, already recovered where it is raised by re-creating the group
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
    /// <remarks>
    ///   - never add <c>BUSY</c> to the set — <c>BUSYGROUP</c> matches that prefix
    ///   - <c>BUSYGROUP</c> is provisioning noise the topology already handles, not a retry case
    /// </remarks>
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
            // ">" reads entries never handed to any consumer in the group, so a fresh delivery is always attempt 1.
            read = await database.StreamReadGroupAsync(positions, options.Value.ConsumerGroup, _consumerName, countPerStream: options.Value.BatchSize);
        }
        catch (RedisServerException exception) when (exception.Message.StartsWith(RedisStreamsTopologyBroker.GroupMissingPrefix, StringComparison.Ordinal))
        {
            // The key or group went away (FLUSHDB, DEL, failover) — rebuild and let the next iteration read.
            LogGroupMissing();
            await _topology.EnsureGroupsAsync(database, [.. positions.Select(position => position.Key.ToString())], options.Value.ConsumerGroup);
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
    ///   - <c>XPENDING</c> before <c>XCLAIM</c> reports each entry's true <see cref="EventEnvelope.DeliveryCount"/>
    ///   - it needs only Redis 5.0, where <c>XAUTOCLAIM</c> needs 6.2
    ///   - the idle time is passed to <c>XCLAIM</c> too, so a losing concurrent sweep declines instead of double-delivering
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

            // A null consumer means every consumer in the group, so this process's own stranded entries recover too.
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
    ///   - <c>XCLAIM</c> returning nothing is not evidence of a phantom — another instance may have claimed it first
    ///   - <c>XACK</c> is scoped to the group, so acking a live entry erases the winner's only record that it is in flight
    ///   - an id is acked only when the stream no longer holds it, which monotonic ids make final
    /// </remarks>
    private async ValueTask PurgePhantomsAsync(IDatabase database, string stream, RedisValue[] requested, HashSet<RedisValue> recovered)
    {
        var unclaimed = requested.Where(id => !recovered.Contains(id)).ToArray();
        if (unclaimed.Length == 0)
            return;

        // One XRANGE per unclaimed id, all issued before the first await so the multiplexer pipelines them.
        var lookups = new Task<StreamEntry[]>[unclaimed.Length];
        for (var index = 0; index < unclaimed.Length; index++)
            lookups[index] = database.StreamRangeAsync(stream, unclaimed[index], unclaimed[index], count: 1);

        var phantoms = new List<RedisValue>(unclaimed.Length);
        for (var index = 0; index < unclaimed.Length; index++)
        {
            // Still in the stream means a live entry another consumer owns, so leave its pending row alone.
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
            // Unsettled entries stay in this consumer's PEL, recovered by the claim path once they go idle.
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
            RedisStreamsWireFormatMapper.BuildDeadLetterEntry(entry, stream, deliveryCount, reason, exception),
            messageId: null,
            maxLength: opt.DeadLetterMaxLength,
            useApproximateMaxLength: opt.UseApproximateMaxLength);

        // Ack only after the DLQ write lands — at worst a later claim duplicates it into the DLQ.
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
                // The terminal fault already surfaced through ExecuteAsync; re-throwing would abort the release below.
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
        // RedisStreamsConnection owns the multiplexer, which the send path may still be flushing through.
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }
    }

    private EventEnvelope? TryReconstruct(string stream, StreamEntry entry, int deliveryCount)
    {
        var headers = RedisStreamsWireFormatMapper.DecodeHeaders(entry);
        if (!headers.TryGetValue(RedisStreamsFieldConstants.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        if (RedisStreamsWireFormatMapper.ReadBody(entry) is not { } data)
            return null;

        // Read the declared content type or nothing — guessing JSON would reach the wrong deserializer.
        var decoded = SerializerFor(ReadOptional(headers, RedisStreamsFieldConstants.ContentType)).Deserialize(data, eventType);
        if (decoded.IsFailure(out _, out var body))
            return null;

        return new EventEnvelope
        {
            MessageId = headers.TryGetValue(RedisStreamsFieldConstants.MessageId, out var id) ? id : Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,
            Destination = stream,
            // 1-based, off the PEL's own delivery count: 1 is the first delivery, not zero prior attempts.
            DeliveryCount = deliveryCount,
            ContentType = headers.TryGetValue(RedisStreamsFieldConstants.ContentType, out var contentType) ? contentType : "application/json",
            PartitionKey = ReadOptional(headers, RedisStreamsFieldConstants.PartitionKey),
            ReplyTo = ReadOptional(headers, MessageHeaderConstants.ReplyTo),
            CorrelationId = ReadOptional(headers, RedisStreamsFieldConstants.CorrelationId),
            ConversationId = ReadOptional(headers, MessageHeaderConstants.ConversationId),
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
