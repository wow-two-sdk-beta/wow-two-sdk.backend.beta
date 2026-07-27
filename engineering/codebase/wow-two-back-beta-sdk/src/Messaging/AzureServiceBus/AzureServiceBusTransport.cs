using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus;

/// <summary>Options for the Azure Service Bus event-bus adapter.</summary>
public sealed class AzureServiceBusOptions
{
    /// <summary>
    /// Service Bus connection string (<c>Endpoint=sb://…;SharedAccessKeyName=…;SharedAccessKey=…</c>). Required — there
    /// is no local default, because unlike a broker you run yourself a Service Bus namespace only exists in Azure.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Topic events are published to. Default <c>wt-events</c>. Created on start when <see cref="ProvisionEntities"/> is on.</summary>
    public string Topic { get; set; } = "wt-events";

    /// <summary>
    /// This service's subscription under <see cref="TopologyStyle.SharedEndpoint"/>. Default <c>wt-events</c>. Ignored
    /// under <see cref="TopologyStyle.EndpointPerMessageType"/>, where every subscription is named by
    /// <see cref="IEndpointNameFormatter"/>.
    /// </summary>
    public string Subscription { get; set; } = "wt-events";

    /// <summary>
    /// Create the topic, this process's subscriptions and their filter rules on start if absent. Default true.
    /// <para>
    /// Turn it off when entities are provisioned out-of-band (Bicep/Terraform, or a namespace whose connection string
    /// carries only Send/Listen rights) — <c>Manage</c> rights are required to provision, and without them every start
    /// would fail on the first management call rather than on a missing entity.
    /// </para>
    /// </summary>
    public bool ProvisionEntities { get; set; } = true;

    /// <summary>
    /// Broker-side delivery cap before Service Bus moves the message to the native dead-letter queue. Default 10.
    /// Applied only to a subscription this adapter creates — Service Bus ignores it on an existing one.
    /// <para>
    /// This is the crash safety-net, not the retry policy: in-process retries are the SDK resilience pipeline's, and the
    /// pipeline normally dead-letters a message explicitly through its <see cref="ReceiveContext"/> long before this
    /// count is reached. It only bites when the process dies mid-handler and the lock expires.
    /// </para>
    /// </summary>
    public int MaxDeliveryCount { get; set; } = 10;

    /// <summary>
    /// Lock held on a received message while it is being processed, on subscriptions this adapter creates. Default 60s,
    /// which is also the Service Bus maximum. A handler outrunning the lock loses it, and settlement then fails with
    /// <see cref="ServiceBusFailureReason.MessageLockLost"/> — keep it above
    /// <see cref="ConcurrencyOptions.DrainTimeout"/> plus the slowest handler, or renew the lock in the handler.
    /// </summary>
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Messages the receiver pre-fetches into memory ahead of the pipeline. Default 0 — no prefetch, so a message is
    /// locked only when it is about to be processed.
    /// <para>
    /// Raising it trades latency for risk: a prefetched message's lock clock is already running while it waits in the
    /// local buffer, so a prefetch larger than the pipeline drains within <see cref="LockDuration"/> expires locks and
    /// redelivers.
    /// </para>
    /// </summary>
    public int PrefetchCount { get; set; }

    /// <summary>Messages requested per receive call. Default 10. A batch is dispatched one message at a time, so this is a network-round-trip knob, not a concurrency one — concurrency is <see cref="ConcurrencyOptions.MaxConcurrentMessages"/>.</summary>
    public int MaxMessagesPerReceive { get; set; } = 10;

    /// <summary>How long a receive call waits for the first message before returning empty and looping. Default 30s. Longer means fewer idle round-trips; it does not delay a message that arrives during the wait.</summary>
    public TimeSpan ReceiveWaitTime { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Consume through sessions: subscriptions this adapter creates are session-enabled, the send path maps
    /// <see cref="EventEnvelope.PartitionKey"/> to <c>SessionId</c>, and the receive path locks one session at a time so
    /// a key's messages are processed in order by a single consumer. Default false.
    /// <para>
    /// Opt-in, and not reversible in place: <c>RequiresSession</c> is fixed when a subscription is created, so turning
    /// this on against an existing non-session subscription needs a new subscription name. It is also what makes
    /// <see cref="ITransportCapabilities.NativeSessions"/> and <see cref="ITransportCapabilities.NativeOrdering"/>
    /// report true — both are properties of a session-enabled entity, not of the namespace.
    /// </para>
    /// </summary>
    public bool RequiresSession { get; set; }

    /// <summary>Sessions locked concurrently per subscription when <see cref="RequiresSession"/> is on. Default 1. Each is an independent accept loop, so this is how a session-ordered deployment scales out across keys.</summary>
    public int MaxConcurrentSessions { get; set; } = 1;

    /// <summary>How long a locked session may sit empty before it is released and the loop accepts the next one. Default 30s. Too short churns session locks; too long starves other sessions of a scarce accept loop.</summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Create the topic with duplicate detection, so Service Bus drops a re-send carrying a
    /// <see cref="EventEnvelope.MessageId"/> it has already seen within <see cref="DuplicateDetectionWindow"/>.
    /// Default false; this is what <see cref="ITransportCapabilities.NativeDedupe"/> tracks.
    /// <para>
    /// Opt-in because it is fixed at topic creation and is not free — the broker keeps a message-id index for the whole
    /// window. The SDK inbox dedupes on the consume side regardless; this dedupes on the produce side, which is what an
    /// ambiguous send retry needs.
    /// </para>
    /// </summary>
    public bool EnableDuplicateDetection { get; set; }

    /// <summary>How far back duplicate detection looks, on a topic this adapter creates with <see cref="EnableDuplicateDetection"/>. Default 10 minutes (the Service Bus default).</summary>
    public TimeSpan DuplicateDetectionWindow { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// On start, drop the <c>$Default</c> catch-all rule from each subscription. Default false. Migration aid only: a
    /// subscription created outside this adapter carries a true-filter rule, so it keeps receiving every message on the
    /// topic alongside the per-type rules. Leave it off once the migration has run.
    /// </summary>
    public bool RemoveCatchAllRule { get; set; }

    /// <summary>Tunnel AMQP over WebSockets (port 443) instead of AMQP over TCP (port 5671). Default false. Turn it on where outbound 5671 is closed.</summary>
    public bool UseWebSockets { get; set; }
}

/// <summary>Well-known Service Bus application-property keys the adapter sets.</summary>
/// <remarks>
/// Deliberately short: Service Bus promotes message id, correlation id, reply-to, content type, TTL, scheduled enqueue
/// time and session id to first-class <see cref="ServiceBusMessage"/> properties, so those never ride a header here.
/// What is left is the metadata AMQP has no property for.
/// </remarks>
internal static class AzureServiceBusHeaders
{
    /// <summary>Carries the event's stable type token so the consumer can resolve the CLR type.</summary>
    public const string EventType = MessageHeaders.EventType;

    /// <summary>Carries the serializer content type. Mirrored onto the native <see cref="ServiceBusMessage.ContentType"/>; the header is what a bridged non-SDK consumer reads.</summary>
    public const string ContentType = MessageHeaders.ContentType;

    /// <summary>Carries the ordering / partition key, so it survives on a non-session entity where <c>SessionId</c> is ignored.</summary>
    public const string PartitionKey = MessageHeaders.PartitionKey;

    /// <summary>Carries <see cref="EventEnvelope.ConversationId"/> — Service Bus has no property for it, and <c>CorrelationId</c> is already spoken for by the business flow.</summary>
    public const string ConversationId = MessageHeaders.ConversationId;
}

/// <summary>
/// Sanitizes a topology routing key into a legal Service Bus entity or rule name. Service Bus accepts letters, digits,
/// periods, hyphens and underscores, and caps a subscription or rule name at 50 characters — far shorter than the
/// namespace-qualified type tokens the topology produces, so a long name is truncated and disambiguated by a hash of
/// the original rather than silently colliding with its siblings.
/// </summary>
internal static class AzureServiceBusEntityName
{
    /// <summary>Service Bus ceiling on a subscription or rule name.</summary>
    public const int MaxSubscriptionLength = 50;

    /// <summary>Service Bus ceiling on a topic (or queue) name.</summary>
    public const int MaxTopicLength = 260;

    /// <summary>Characters reserved for the hash suffix on a truncated name: a separator plus eight hex digits.</summary>
    private const int HashSuffixLength = 9;

    /// <summary>The legal name for <paramref name="value"/>, truncated with a stable hash suffix when it exceeds <paramref name="maxLength"/>.</summary>
    /// <param name="value">A routing key, endpoint name, or other candidate name.</param>
    /// <param name="maxLength">The Service Bus ceiling for the entity kind being named.</param>
    public static string Sanitize(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            // ASCII only, deliberately: char.IsLetterOrDigit admits non-ASCII letters that the management API rejects.
            // Everything else collapses to '-', so two distinct keys stay distinct names.
            var legal = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_';
            builder.Append(legal ? character : '-');
        }

        // A name may not start or end with a separator; trimming can empty the string, which is not a name at all.
        var name = builder.ToString().Trim('.', '-', '_');
        if (name.Length == 0)
            name = "wt";

        if (name.Length <= maxLength)
            return name;

        // Truncation alone would map `Contracts.Orders.OrderPlaced` and `Contracts.Orders.OrderPlacedV2` onto one rule,
        // silently routing one type's messages by another's filter. The hash is over the FULL name, so the pair stays
        // distinct, and it is stable across processes and restarts — the filter must survive a redeploy unchanged.
        var hash = StableHash(name).ToString("x8", CultureInfo.InvariantCulture);
        // Span TrimEnd takes a set as one span, not params chars.
        return string.Concat(name.AsSpan(0, maxLength - HashSuffixLength).TrimEnd(".-_".AsSpan()), "-", hash);
    }

    // FNV-1a: string.GetHashCode is randomised per process, and a rule name that moved on restart would orphan the
    // rule it created last time and add a second one beside it on every deploy.
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= 16777619u;
        }

        return hash;
    }
}

/// <summary>Lazily-opened shared <see cref="ServiceBusClient"/> (singleton) — the send and receive halves share one AMQP connection, as the client is designed for.</summary>
internal sealed class AzureServiceBusConnection(IOptions<AzureServiceBusOptions> options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ServiceBusClient? _client;

    public async ValueTask<ServiceBusClient> GetClientAsync(CancellationToken cancellationToken)
    {
        // No IsClosed gate on the fast path. The client reconnects its own AMQP links after a network drop and retries
        // transient faults internally, so a live client is never replaced here — only an explicitly disposed one is
        // closed, and that happens at shutdown.
        if (_client is not null)
            return _client;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null)
                return _client;

            var opt = options.Value;
            ArgumentException.ThrowIfNullOrWhiteSpace(opt.ConnectionString, nameof(AzureServiceBusOptions.ConnectionString));

            _client = new ServiceBusClient(opt.ConnectionString, new ServiceBusClientOptions
            {
                TransportType = opt.UseWebSockets ? ServiceBusTransportType.AmqpWebSockets : ServiceBusTransportType.AmqpTcp,
            });

            return _client;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
            await _client.DisposeAsync();
        _gate.Dispose();
    }
}

/// <summary>
/// Idempotent topic / subscription / rule provisioning. Every call swallows "already exists", so concurrent instances
/// racing at startup all succeed and an existing deployment's entity settings are never rewritten.
/// </summary>
internal sealed partial class AzureServiceBusTopology(IOptions<AzureServiceBusOptions> options, ILogger<AzureServiceBusTopology> logger)
{
    /// <summary>Provision the topic, and for each endpoint its subscription plus one correlation rule per routing key.</summary>
    public async ValueTask ProvisionAsync(IReadOnlyList<EndpointTopology> endpoints, CancellationToken cancellationToken)
    {
        var opt = options.Value;
        var admin = new ServiceBusAdministrationClient(opt.ConnectionString);
        var topic = AzureServiceBusEntityName.Sanitize(opt.Topic, AzureServiceBusEntityName.MaxTopicLength);

        await CreateTopicAsync(admin, opt, topic, cancellationToken);

        foreach (var endpoint in endpoints)
        {
            var subscription = AzureServiceBusEntityName.Sanitize(endpoint.Queue, AzureServiceBusEntityName.MaxSubscriptionLength);
            await CreateSubscriptionAsync(admin, opt, topic, subscription, cancellationToken);
            await SyncRulesAsync(admin, opt, topic, subscription, endpoint.RoutingKeys, cancellationToken);
        }
    }

    private static async ValueTask CreateTopicAsync(ServiceBusAdministrationClient admin, AzureServiceBusOptions opt, string topic, CancellationToken cancellationToken)
    {
        var createOptions = new CreateTopicOptions(topic)
        {
            // Duplicate detection is immutable after creation, which is why it is an option rather than always-on: an
            // existing topic keeps whatever it was created with, and this only governs a topic created here.
            RequiresDuplicateDetection = opt.EnableDuplicateDetection,
        };

        if (opt.EnableDuplicateDetection)
            createOptions.DuplicateDetectionHistoryTimeWindow = opt.DuplicateDetectionWindow;

        await TryCreateAsync(async () => await admin.CreateTopicAsync(createOptions, cancellationToken));
    }

    private static async ValueTask CreateSubscriptionAsync(ServiceBusAdministrationClient admin, AzureServiceBusOptions opt, string topic, string subscription, CancellationToken cancellationToken)
    {
        var createOptions = new CreateSubscriptionOptions(topic, subscription)
        {
            RequiresSession = opt.RequiresSession,
            MaxDeliveryCount = opt.MaxDeliveryCount,
            LockDuration = opt.LockDuration,

            // A message that outlives its TTL goes to the dead-letter queue instead of vanishing. The envelope's TTL is
            // a caller hint, so an expiry is a fact worth keeping rather than a silent drop.
            DeadLetteringOnMessageExpiration = true,
        };

        // Created with a false filter, not the default true one: the rules synced immediately below are what this
        // subscription should match, and a subscription that briefly matched everything would have already pulled in
        // every other service's messages before the real rules landed.
        var seedRule = new CreateRuleOptions(RuleProperties.DefaultRuleName, new FalseRuleFilter());
        await TryCreateAsync(async () => await admin.CreateSubscriptionAsync(createOptions, seedRule, cancellationToken));
    }

    /// <summary>
    /// Ensure exactly one correlation rule per routing key. A correlation filter on <c>Subject</c> is the Service Bus
    /// analogue of a RabbitMQ topic binding — the broker evaluates it against an indexed property, so it is the
    /// cheapest filter kind, and it keeps the send path's routing key the single source of what a subscriber receives.
    /// </summary>
    private async ValueTask SyncRulesAsync(
        ServiceBusAdministrationClient admin,
        AzureServiceBusOptions opt,
        string topic,
        string subscription,
        IReadOnlyList<string> routingKeys,
        CancellationToken cancellationToken)
    {
        foreach (var routingKey in routingKeys)
        {
            var ruleName = AzureServiceBusEntityName.Sanitize(routingKey, AzureServiceBusEntityName.MaxSubscriptionLength);

            // The seed rule owns $Default; a routing key sanitizing to that name would replace the false filter with a
            // real one and leave the subscription correct but undiagnosable. Skip rather than fight over the name.
            if (string.Equals(ruleName, RuleProperties.DefaultRuleName, StringComparison.OrdinalIgnoreCase))
                continue;

            var rule = new CreateRuleOptions(ruleName, new CorrelationRuleFilter(routingKey));
            await TryCreateAsync(async () => await admin.CreateRuleAsync(topic, subscription, rule, cancellationToken));
        }

        if (!opt.RemoveCatchAllRule)
            return;

        // A subscription provisioned outside this adapter carries the true-filter $Default rule, so it keeps matching
        // every message on the topic no matter what per-type rules exist beside it. Best-effort: the whole point is to
        // remove a rule that may well already be gone.
        try
        {
            await admin.DeleteRuleAsync(topic, subscription, RuleProperties.DefaultRuleName, cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            // Already removed, or the seed rule this adapter created was never a catch-all to begin with.
        }
        catch (ServiceBusException ex)
        {
            LogCatchAllRuleRemovalFailed(subscription, ex);
        }
    }

    /// <summary>Run a management create, treating "already exists" as success — the entity is provisioned, which is all the caller asked for.</summary>
    private static async ValueTask TryCreateAsync(Func<Task> create)
    {
        try
        {
            await create();
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
        {
            // Provisioned by an earlier start or by another instance racing this one.
        }
    }

    [LoggerMessage(EventId = 6710, Level = LogLevel.Warning, Message = "Removing the $Default catch-all rule from Service Bus subscription {Subscription} failed; it may still receive message types this service does not handle")]
    private partial void LogCatchAllRuleRemovalFailed(string subscription, Exception exception);
}

/// <summary>
/// Azure Service Bus <see cref="ISendTransport"/> — publishes envelopes to a topic, mapping every transport-abstract
/// hint the envelope carries onto the Service Bus property that natively implements it: scheduled enqueue time, TTL,
/// session id, reply-to and correlation id are message properties, not headers. The <c>Subject</c> carries the routing
/// key <see cref="ITopologyProvider"/> resolves, which is what subscription correlation filters match on.
/// </summary>
internal sealed class AzureServiceBusSendTransport(
    AzureServiceBusConnection connection,
    IOptions<AzureServiceBusOptions> options,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ITopologyProvider topology) : ISendTransport, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ServiceBusSender? _sender;

    public async ValueTask SendAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var sender = await GetSenderAsync(cancellationToken);
        var body = envelope.ToWireBody(serializer);

        var message = new ServiceBusMessage(new BinaryData(body))
        {
            MessageId = envelope.MessageId,
            CorrelationId = envelope.CorrelationId,
            ContentType = serializer.ContentType,

            // The routing key rides Subject because that is what a subscription's correlation filter matches — the
            // Service Bus equivalent of publishing under a routing key that consumers bind. Deliberately the raw
            // topology key on both sides: AzureServiceBusEntityName sanitizes the rule's NAME, never the value, so the
            // string a CorrelationRuleFilter was built from is by construction the string sent here.
            Subject = topology.ResolveRoutingKey(envelope),

            // Native reply address. Left null for a one-way message so the property is absent from the AMQP frame
            // rather than present-and-empty.
            ReplyTo = string.IsNullOrEmpty(envelope.ReplyTo) ? null : envelope.ReplyTo,
        };

        // Caller headers first, minus the reserved namespace, then the control properties — the adapter owns wt-*. A
        // received envelope's Headers carry the wt-* keys of THAT message, so forwarding them on a re-publish would
        // otherwise stamp the previous message's type token onto the new body and misroute it on the consumer.
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaders.IsAdapterOwned(key))
                message.ApplicationProperties[key] = value;

        // WireBodyType, not BodyType: a send-path transformation that substituted the wire bytes decodes as a different
        // shape, and this token is what the receiver deserializes into. The Subject routing key above stays on BodyType.
        message.ApplicationProperties[AzureServiceBusHeaders.EventType] = typeResolver.ToTypeToken(envelope.WireBodyType);
        message.ApplicationProperties[AzureServiceBusHeaders.ContentType] = serializer.ContentType;

        if (!string.IsNullOrEmpty(envelope.ConversationId))
            message.ApplicationProperties[AzureServiceBusHeaders.ConversationId] = envelope.ConversationId;

        ApplyOrderingKey(message, envelope);

        // Native per-message TTL. Service Bus silently clamps it down to the entity's DefaultMessageTimeToLive, so a
        // hint longer than the topic allows shortens rather than fails. Non-positive is meaningless and would be
        // rejected outright, so it is dropped and the entity default applies.
        if (envelope.TimeToLive is { } timeToLive && timeToLive > TimeSpan.Zero)
            message.TimeToLive = timeToLive;

        // Native scheduled delivery: the broker holds the message and reveals it at this instant, surviving a restart
        // on either side. NotBeforeUtc is the only field to read — IEventBus turns a relative Delay into this absolute
        // time before the envelope ever reaches a transport. A time already past is dropped rather than sent, since
        // Service Bus would enqueue it immediately anyway and the property would only misreport history.
        if (envelope.NotBeforeUtc is { } notBefore && notBefore > DateTimeOffset.UtcNow)
            message.ScheduledEnqueueTime = notBefore;

        // No retry wrapped around this call: the client already retries transient faults internally, and a resend after
        // an ambiguous failure duplicates the message unless the topic has duplicate detection on. The failure goes to
        // the caller, which owns the idempotency decision.
        await sender.SendMessageAsync(message, cancellationToken);
    }

    /// <summary>
    /// Map the envelope's ordering key onto whichever Service Bus property actually orders on this entity.
    /// </summary>
    /// <remarks>
    /// The two are mutually exclusive, not complementary. On a session-enabled entity <c>SessionId</c> IS the partition
    /// key, and setting <c>PartitionKey</c> to a different value is rejected outright; on a session-unaware entity
    /// <c>SessionId</c> is ignored and only <c>PartitionKey</c> groups messages onto one partition. Writing whichever
    /// one the configured entity honours — and the header always, so the key survives to the consumer either way — is
    /// what keeps one envelope field working on both shapes.
    /// </remarks>
    private void ApplyOrderingKey(ServiceBusMessage message, EventEnvelope envelope)
    {
        if (string.IsNullOrEmpty(envelope.PartitionKey))
            return;

        if (options.Value.RequiresSession)
            message.SessionId = envelope.PartitionKey;
        else
            message.PartitionKey = envelope.PartitionKey;

        message.ApplicationProperties[AzureServiceBusHeaders.PartitionKey] = envelope.PartitionKey;
    }

    private async ValueTask<ServiceBusSender> GetSenderAsync(CancellationToken cancellationToken)
    {
        if (_sender is { IsClosed: false })
            return _sender;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_sender is { IsClosed: false })
                return _sender;

            var client = await connection.GetClientAsync(cancellationToken);
            var topic = AzureServiceBusEntityName.Sanitize(options.Value.Topic, AzureServiceBusEntityName.MaxTopicLength);

            // The sender is designed to be cached for the life of the application — it owns the AMQP link and reopens
            // it after a drop, so creating one per send would pay a link handshake on every message.
            _sender = client.CreateSender(topic);
            return _sender;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_sender is not null)
            await _sender.DisposeAsync();
        _gate.Dispose();
    }
}

/// <summary>
/// Azure Service Bus <see cref="ReceiveContext"/> — settles through the receiver that delivered the message, which is
/// what lets settlement run from a pump worker rather than on the consume loop. Dead-lettering is native: the broker
/// moves the message to the entity's <c>$DeadLetterQueue</c> sub-queue and records the reason on it, so nothing is
/// re-published onto a second queue the way an emulated DLQ has to.
/// </summary>
internal sealed class AzureServiceBusReceiveContext(
    EventEnvelope envelope,
    ServiceBusReceiver receiver,
    ServiceBusReceivedMessage message) : ReceiveContext
{
    /// <summary>Service Bus ceiling on the dead-letter reason and description properties.</summary>
    private const int MaxDeadLetterTextLength = 4096;

    public override EventEnvelope Envelope => envelope;

    public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
        => new(receiver.CompleteMessageAsync(message, cancellationToken));

    public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
        // Native dead-letter with reason + description, so the record is on the message in the DLQ itself and readable
        // by any tool that peeks it — no wt-dl-* headers, and no re-produce onto a separate queue.
        => new(receiver.DeadLetterMessageAsync(
            message,
            Truncate(reason),
            Truncate(exception is null ? reason : $"{exception.GetType().FullName}: {exception.Message}"),
            cancellationToken));

    /// <summary>Clip text to the Service Bus limit — an over-long reason fails the settle call, turning a dead-letter into a lock timeout and a redelivery.</summary>
    private static string Truncate(string text)
        => text.Length <= MaxDeadLetterTextLength ? text : text[..MaxDeadLetterTextLength];
}

/// <summary>
/// Azure Service Bus <see cref="IReceiveTransport"/> — provisions the topology, then runs a receive loop per endpoint
/// subscription (or per concurrent session, when <see cref="AzureServiceBusOptions.RequiresSession"/> is on) and routes
/// each message into the pipeline.
/// </summary>
/// <remarks>
/// Built on <see cref="ServiceBusReceiver"/> rather than <see cref="ServiceBusProcessor"/> on purpose. The processor
/// stops renewing a message's lock once its handler returns, so settling after that point — which is exactly what the
/// pump does when it dispatches to a worker — would race lock expiry. The receiver holds the lock for the entity's
/// configured duration regardless of who settles it or from which thread, which is what makes
/// <see cref="ITransportCapabilities.SettlesInContext"/> honest here — on the non-session path. A session receiver is
/// released when its session drains, so off-loop settlement has a window there and the capability reports false; see
/// <see cref="AzureServiceBusCapabilities.SettlesInContext"/>.
/// </remarks>
internal sealed partial class AzureServiceBusReceiveTransport(
    AzureServiceBusConnection connection,
    IOptions<AzureServiceBusOptions> options,
    AzureServiceBusTopology topology,
    ITopologyProvider topologyProvider,
    IMessageSerializer serializer,
    IMessageTypeResolver typeResolver,
    ILogger<AzureServiceBusReceiveTransport> logger,
    MessageSerializerRegistry? serializerRegistry = null) : IReceiveTransport, IAsyncDisposable
{
    /// <summary>Pause before a receive loop retries after a non-transient failure. The client already retries transient faults, so reaching here means something the loop should back off from rather than spin on.</summary>
    private static readonly TimeSpan FaultBackoff = TimeSpan.FromSeconds(5);

    /// <summary>Receivers opened once per endpoint and kept for the life of the transport. Disposed in <see cref="StopAsync"/>, never when a loop exits — a pump worker may still be settling on one.</summary>
    private readonly ConcurrentDictionary<ServiceBusReceiver, byte> _receivers = new();

    private CancellationTokenSource? _cts;

    public async ValueTask StartAsync(Func<ReceiveContext, CancellationToken, ValueTask> onMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        var endpoints = topologyProvider.ConsumeEndpoints;

        // Created before the branch below, not after it: StopAsync stops this transport by cancelling this source, so
        // anything awaited on a token it cannot reach is unstoppable that way. The hosted service also cancels the host
        // token, which masks it — but a direct StopAsync (a test, an explicit bus teardown) would hang forever.
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        try
        {
            // No endpoints means no handler is registered and the topology is per-type, so there is nothing to
            // provision and nothing to consume. Park rather than return: returning would complete the hosted service's
            // ExecuteAsync and read as a clean shutdown of the consumer.
            if (endpoints.Count == 0)
            {
                LogNoConsumeEndpoints();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return;
            }

            if (options.Value.ProvisionEntities)
                await topology.ProvisionAsync(endpoints, token);

            var client = await connection.GetClientAsync(token);
            var topic = AzureServiceBusEntityName.Sanitize(options.Value.Topic, AzureServiceBusEntityName.MaxTopicLength);

            var loops = new List<Task>();
            foreach (var endpoint in endpoints)
            {
                var subscription = AzureServiceBusEntityName.Sanitize(endpoint.Queue, AzureServiceBusEntityName.MaxSubscriptionLength);
                LogConsumingSubscription(subscription, options.Value.RequiresSession);

                if (options.Value.RequiresSession)
                {
                    // One accept loop per concurrent session: a session receiver owns exactly one session at a time, so
                    // the only way to process N keys in parallel is N receivers each holding its own session lock.
                    var sessions = Math.Max(1, options.Value.MaxConcurrentSessions);
                    for (var index = 0; index < sessions; index++)
                        loops.Add(Task.Run(() => RunSessionLoopAsync(client, subscription, onMessage, token), CancellationToken.None));
                }
                else
                {
                    var receiver = client.CreateReceiver(topic, subscription, ReceiverOptions());
                    _receivers.TryAdd(receiver, 0);
                    loops.Add(Task.Run(() => RunReceiveLoopAsync(receiver, subscription, onMessage, token), CancellationToken.None));
                }
            }

            // Awaited, not fired and forgotten: this method IS the hosted service's ExecuteAsync body, so returning
            // early would report the consumer as finished while its loops were still running.
            await Task.WhenAll(loops);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Graceful shutdown, whether it came from the host token or from StopAsync — the linked source reports both.
            // Receivers are deliberately left open: TransportConsumerHostedService drains handlers still in flight after
            // this returns, and they settle on these receivers before StopAsync releases them.
        }
    }

    private ServiceBusReceiverOptions ReceiverOptions() => new()
    {
        // PeekLock, never ReceiveAndDelete: the pipeline's whole contract is that a message is settled after it has been
        // processed, and ReceiveAndDelete settles it at delivery — a crash mid-handler would lose it silently.
        ReceiveMode = ServiceBusReceiveMode.PeekLock,
        PrefetchCount = Math.Max(0, options.Value.PrefetchCount),
    };

    /// <summary>Pull batches from one subscription until cancelled, dispatching each message into the pipeline.</summary>
    private async Task RunReceiveLoopAsync(
        ServiceBusReceiver receiver,
        string subscription,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var batch = await receiver.ReceiveMessagesAsync(
                    Math.Max(1, options.Value.MaxMessagesPerReceive),
                    options.Value.ReceiveWaitTime,
                    cancellationToken);

                foreach (var message in batch)
                    await DispatchAsync(receiver, message, onMessage, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The loop must outlive a failed receive: faulting here would stop consuming this subscription for the
                // life of the process over an outage the namespace will recover from.
                LogReceiveFailed(subscription, ex);
                await DelayAsync(FaultBackoff, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Lock one session at a time and drain it, then release it and accept the next. Releasing on idle rather than
    /// holding the lock is what stops a small pool of accept loops from pinning itself to quiet sessions while busy
    /// ones wait.
    /// </summary>
    private async Task RunSessionLoopAsync(
        ServiceBusClient client,
        string subscription,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        var topic = AzureServiceBusEntityName.Sanitize(options.Value.Topic, AzureServiceBusEntityName.MaxTopicLength);

        while (!cancellationToken.IsCancellationRequested)
        {
            ServiceBusSessionReceiver? receiver = null;
            try
            {
                receiver = await client.AcceptNextSessionAsync(
                    topic,
                    subscription,
                    new ServiceBusSessionReceiverOptions
                    {
                        ReceiveMode = ServiceBusReceiveMode.PeekLock,
                        PrefetchCount = Math.Max(0, options.Value.PrefetchCount),
                    },
                    cancellationToken);

                _receivers.TryAdd(receiver, 0);
                await DrainSessionAsync(receiver, onMessage, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                // No session had messages within the accept window. Ordinary idle, not a fault — loop straight back
                // round rather than logging or backing off.
                continue;
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.SessionLockLost)
            {
                // The session lock expired under a slow handler. The messages are unsettled, so Service Bus re-offers
                // the session; the inbox dedupes whatever was already processed.
                LogSessionLockLost(subscription);
            }
            catch (Exception ex)
            {
                LogReceiveFailed(subscription, ex);
                await DelayAsync(FaultBackoff, cancellationToken);
            }
            finally
            {
                // Released only on a clean rotation. On cancellation the receiver stays open and registered so handlers
                // draining on pump workers can still settle through it; StopAsync disposes it once they are done.
                if (receiver is not null && !cancellationToken.IsCancellationRequested)
                {
                    _receivers.TryRemove(receiver, out _);
                    await DisposeQuietlyAsync(receiver);
                }
            }
        }
    }

    /// <summary>Pull from a locked session until it runs dry, then hand the loop back so the lock can be released.</summary>
    private async Task DrainSessionAsync(
        ServiceBusSessionReceiver receiver,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await receiver.ReceiveMessagesAsync(
                Math.Max(1, options.Value.MaxMessagesPerReceive),
                options.Value.SessionIdleTimeout,
                cancellationToken);

            if (batch.Count == 0)
                return;

            foreach (var message in batch)
                await DispatchAsync(receiver, message, onMessage, cancellationToken);
        }
    }

    /// <summary>Reconstruct one message and hand it to the pipeline; dead-letter it natively when it cannot be reconstructed.</summary>
    private async ValueTask DispatchAsync(
        ServiceBusReceiver receiver,
        ServiceBusReceivedMessage message,
        Func<ReceiveContext, CancellationToken, ValueTask> onMessage,
        CancellationToken cancellationToken)
    {
        var envelope = TryReconstruct(message);
        if (envelope is null)
        {
            // Native DLQ makes this strictly better than the emulated-DLQ transports can manage: the unparseable
            // message is preserved with its reason attached instead of being dropped or re-produced.
            LogUnparseable(message.MessageId);
            await receiver.DeadLetterMessageAsync(message, "unparseable", "The message carries no resolvable wt-event-type, or its body failed to deserialize", cancellationToken);
            return;
        }

        try
        {
            // The pipeline settles via the context (ctx.Acknowledge on success / ctx.DeadLetter on exhaustion).
            await onMessage(new AzureServiceBusReceiveContext(envelope, receiver, message), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Unsettled → the lock expires and Service Bus redelivers, capped by MaxDeliveryCount and deduped by the
            // SDK inbox. Swallowed so one poison message cannot stop the loop.
            LogProcessingError(envelope.MessageId, ex);
        }
    }

    private EventEnvelope? TryReconstruct(ServiceBusReceivedMessage message)
    {
        var headers = DecodeHeaders(message.ApplicationProperties);
        if (!headers.TryGetValue(AzureServiceBusHeaders.EventType, out var typeName) || typeResolver.ResolveType(typeName) is not { } eventType)
            return null;

        // Decoded by whoever encoded it. Selection reads "declared or nothing" — the reserved header, then the native
        // property — never the "application/json" the envelope below falls back to: a producer that stamped neither is
        // one whose format we do not know, and guessing JSON would route a legacy body to the wrong deserializer
        // wherever the default is not JSON.
        var declaredContentType = ReadOptional(headers, AzureServiceBusHeaders.ContentType) ?? message.ContentType;
        var body = SerializerFor(declaredContentType).Deserialize(message.Body.ToMemory().Span, eventType);
        if (body is null)
            return null;

        return new EventEnvelope
        {
            // Native property first, then the reserved header a broker without a message-id property would have used to
            // carry it. A fresh GUID is the last resort only: an id the sender never chose cannot match anything the
            // inbox has seen, so falling back to one straight away would silently disable dedupe for bridged traffic.
            MessageId = FirstNonEmpty(message.MessageId, ReadOptional(headers, MessageHeaders.MessageId)) ?? Guid.NewGuid().ToString("N"),
            Body = body,
            BodyType = eventType,

            // Subject is the routing key the send path stamped, which is what a subscription filter matched — the
            // closest thing to "where this was addressed" Service Bus carries.
            Destination = message.Subject ?? string.Empty,
            CorrelationId = message.CorrelationId,

            // Native delivery count, incremented by the broker on every lock expiry or abandon. Starts at 1 for a first
            // delivery, which is the same convention the envelope uses.
            DeliveryCount = message.DeliveryCount,
            ContentType = headers.TryGetValue(AzureServiceBusHeaders.ContentType, out var contentType) ? contentType : message.ContentType ?? "application/json",

            // Session id first: on a session entity it is the key the broker actually ordered by, and it is what the
            // pump must route on to keep that order through a parallel dispatch. The header is the fallback for a
            // non-session entity, where SessionId is never populated.
            PartitionKey = FirstNonEmpty(message.SessionId, message.PartitionKey, ReadOptional(headers, AzureServiceBusHeaders.PartitionKey)),

            // Native properties are what this adapter writes, so they are read first; the reserved header is accepted as
            // a fallback so a message bridged in from a broker with no such property still correlates.
            ReplyTo = FirstNonEmpty(message.ReplyTo, ReadOptional(headers, MessageHeaders.ReplyTo)),
            ConversationId = ReadOptional(headers, AzureServiceBusHeaders.ConversationId),
            TimeToLive = message.TimeToLive == TimeSpan.MaxValue ? null : message.TimeToLive,
            Headers = headers,
        };
    }

    private static Dictionary<string, string> DecodeHeaders(IReadOnlyDictionary<string, object> properties)
    {
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            decoded[key] = value switch
            {
                string text => text,
                byte[] bytes => Encoding.UTF8.GetString(bytes),

                // AMQP carries typed values; the envelope's header bag is string-keyed and string-valued, so anything
                // else is rendered invariantly rather than with the consuming machine's locale.
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty,
            };
        }

        return decoded;
    }

    private static string? ReadOptional(Dictionary<string, string> headers, string key)
        => headers.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;

    /// <summary>The deserializer for a received content type. Falls back to the injected serializer whenever no registry is wired, which is what keeps a single-serializer container behaving exactly as it did.</summary>
    private IMessageSerializer SerializerFor(string? contentType) => serializerRegistry?.Resolve(contentType) ?? serializer;

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
            if (!string.IsNullOrEmpty(candidate))
                return candidate;

        return null;
    }

    /// <summary>Delay that treats cancellation as a normal wake-up, so a backoff cannot throw out of a loop that is shutting down.</summary>
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

    private async ValueTask DisposeQuietlyAsync(ServiceBusReceiver receiver)
    {
        try
        {
            await receiver.DisposeAsync();
        }
        catch (Exception ex)
        {
            // Best-effort: a receiver whose link is already gone cannot be closed politely, and that is why it is being
            // released in the first place.
            LogReceiverDisposeFailed(ex);
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is { } cts)
        {
            await cts.CancelAsync();
            cts.Dispose();
            _cts = null;
        }

        foreach (var receiver in _receivers.Keys)
        {
            _receivers.TryRemove(receiver, out _);
            await DisposeQuietlyAsync(receiver);
        }

        // The ServiceBusClient itself is the shared singleton's, not this transport's — disposing it here would close
        // the AMQP connection the send transport is still publishing on.
    }

    [LoggerMessage(EventId = 6701, Level = LogLevel.Warning, Message = "Dead-lettering unparseable Service Bus message {MessageId}")]
    private partial void LogUnparseable(string? messageId);

    [LoggerMessage(EventId = 6702, Level = LogLevel.Error, Message = "Service Bus message {MessageId} processing failed; not settled (the lock will expire and redeliver)")]
    private partial void LogProcessingError(string messageId, Exception exception);

    [LoggerMessage(EventId = 6703, Level = LogLevel.Error, Message = "Receiving from Service Bus subscription {Subscription} failed; retrying after a backoff")]
    private partial void LogReceiveFailed(string subscription, Exception exception);

    [LoggerMessage(EventId = 6704, Level = LogLevel.Information, Message = "Consuming Service Bus subscription {Subscription} (sessions {RequiresSession})")]
    private partial void LogConsumingSubscription(string subscription, bool requiresSession);

    [LoggerMessage(EventId = 6705, Level = LogLevel.Warning, Message = "Service Bus session lock lost on subscription {Subscription}; the session will be re-offered and its unsettled messages redelivered")]
    private partial void LogSessionLockLost(string subscription);

    [LoggerMessage(EventId = 6706, Level = LogLevel.Debug, Message = "Disposing a Service Bus receiver failed; it is already closed")]
    private partial void LogReceiverDisposeFailed(Exception exception);

    [LoggerMessage(EventId = 6707, Level = LogLevel.Warning, Message = "No Service Bus consume endpoints: no IEventHandler<> is registered, so there is nothing to provision or consume")]
    private partial void LogNoConsumeEndpoints();
}

/// <summary>
/// Azure Service Bus capability flags — the broker with the most genuinely native support of any adapter here: a real
/// dead-letter queue, absolute-time scheduling, per-message TTL, and sessions. Ordering, sessions and dedupe are
/// properties of how the entity was created, not of the namespace, so those three track the options that create it.
/// </summary>
internal sealed class AzureServiceBusCapabilities(IOptions<AzureServiceBusOptions> options) : ITransportCapabilities
{
    /// <summary>
    /// Every queue and subscription has a <c>$DeadLetterQueue</c> sub-queue, and <c>DeadLetterMessageAsync</c> moves the
    /// message there with a reason and description recorded on it. Nothing is re-produced onto a second entity the way
    /// Kafka and NATS must, and the dead letter keeps its original message id and properties.
    /// </summary>
    public bool NativeDeadLetter => true;

    /// <summary>
    /// A relative delay is expressed as <c>ScheduledEnqueueTime</c> — the send path adds the delay to now and the broker
    /// holds the message until then. Durable at the broker, so it survives a restart on either side rather than living
    /// in a process-local timer.
    /// </summary>
    public bool NativeDelay => true;

    /// <summary>
    /// Duplicate detection is real — the broker drops a re-send of a <c>MessageId</c> seen within the detection window —
    /// but it is fixed when the topic is created, so an existing topic without it silently accepts duplicates. Tracks
    /// <see cref="AzureServiceBusOptions.EnableDuplicateDetection"/> rather than reporting a flat true, so a pipeline
    /// choosing native-versus-emulated does not pick native on the strength of a feature this namespace never enabled.
    /// </summary>
    public bool NativeDedupe => options.Value.EnableDuplicateDetection;

    /// <summary>
    /// FIFO is a session guarantee, not a queue one. Without sessions, competing consumers plus lock expiry and
    /// redelivery reorder a subscription freely — Service Bus documents sessions as the way to get ordering — so this
    /// tracks <see cref="AzureServiceBusOptions.RequiresSession"/>.
    /// </summary>
    public bool NativeOrdering => options.Value.RequiresSession;

    /// <summary>
    /// No per-message priority. Service Bus has no priority property and does not rank a queue by one; the documented
    /// pattern is a subscription per priority band with a filter on a custom property, which is the emulation this flag
    /// exists to distinguish from native support.
    /// </summary>
    public bool NativePriority => false;

    /// <summary>
    /// Native per-message <c>TimeToLive</c>, independent of (and clamped by) the entity's
    /// <c>DefaultMessageTimeToLive</c>. An expired message is dead-lettered rather than dropped, since the adapter
    /// creates subscriptions with <c>DeadLetteringOnMessageExpiration</c>.
    /// </summary>
    public bool NativeTimeToLive => true;

    /// <summary>
    /// <c>ScheduledEnqueueTime</c> — an absolute UTC instant the broker holds the message until, the facility this
    /// flag's own definition cites. Durable and cancellable server-side, so "at 09:00 tomorrow" survives a restart.
    /// </summary>
    public bool NativeScheduling => true;

    /// <summary>
    /// Sessions give exactly what this flag asks for — an exclusive lock over a group of related messages, held by one
    /// consumer, with session state it can carry across them. Conditional, because <c>RequiresSession</c> is fixed when
    /// the entity is created: a session receiver against a non-session subscription fails outright, so this tracks
    /// <see cref="AzureServiceBusOptions.RequiresSession"/>.
    /// </summary>
    public bool NativeSessions => options.Value.RequiresSession;

    /// <summary>
    /// AMQP settled transfer: <c>SendMessageAsync</c> completes only once the broker has accepted and durably stored the
    /// message, and a rejection surfaces as a <see cref="ServiceBusException"/>. A successful send is a real durability
    /// guarantee, not a local hand-off.
    /// </summary>
    public bool NativePublisherConfirms => true;

    /// <summary>
    /// Reported false against this flag's exactly-once bar. Service Bus does have transactions — an ambient
    /// <c>TransactionScope</c> commits a batch of sends atomically, and cross-entity transactions extend that to a
    /// settle-and-send pair — but there is no idempotent producer: a retry after an ambiguous commit re-sends, and only
    /// duplicate detection (a separate, window-bounded feature) would absorb it. Atomic batching without producer
    /// idempotency is exactly what the flag's definition excludes.
    /// </summary>
    public bool NativeTransactions => false;

    /// <summary>
    /// <c>ReplyTo</c> and <c>ReplyToSessionId</c> are first-class message properties, and <c>CorrelationId</c> pairs the
    /// reply with its request — the adapter maps the reply address in both directions rather than smuggling it through a
    /// header, as the transports without such a property must.
    /// </summary>
    /// <remarks>
    /// The reply address must name a real entity: Service Bus has no zero-provisioning pseudo-queue like RabbitMQ's
    /// direct-reply-to or a NATS <c>_INBOX</c>. That matches how <c>IRequestClient</c> actually routes replies on every
    /// adapter here — to an ordinary consumed endpoint — so it does not change what this flag reports.
    /// </remarks>
    public bool NativeRequestReply => true;

    /// <summary>
    /// Settlement goes through <see cref="ServiceBusReceiver"/>, which holds the message lock for the entity's
    /// configured duration no matter which thread settles it — so a pump worker may complete or dead-letter a message
    /// the consume loop received. This is why the adapter is built on the receiver rather than
    /// <see cref="ServiceBusProcessor"/>, whose lock renewal ends when its handler returns.
    /// </summary>
    /// <remarks>
    /// Conditional, so reported false under <see cref="AzureServiceBusOptions.RequiresSession"/>. A session receiver's
    /// message locks live inside the <em>session</em> lock, and the accept loop releases that lock — by disposing the
    /// receiver — as soon as the session drains, which the loop can observe while a pump worker still holds an
    /// unsettled message from it. The settle then fails on a disposed receiver or a lost session lock, leaving the
    /// message to redeliver. Ordinary (non-session) receivers have no such window: they live until
    /// <c>StopAsync</c>, which the hosted service calls only after the drain.
    /// </remarks>
    public bool SettlesInContext => !options.Value.RequiresSession;

    /// <summary>The receive loop is an ordinary async batch pull with no thread affinity, so the pump may dispatch in parallel.</summary>
    public bool ThreadAffineConsume => false;
}

/// <summary>DI registration for the Azure Service Bus event-bus adapter.</summary>
public static class AzureServiceBusServiceCollectionExtensions
{
    /// <summary>
    /// Register the Azure Service Bus transport behind the SDK event bus: send/receive transports (topic +
    /// subscription-per-endpoint with correlation filters, native DLQ), the shared <see cref="IEventBus"/>, resilience
    /// defaults, topology, and the consumer hosted service. Scans the supplied assemblies (or the caller's) for
    /// <see cref="IEventHandler{TEvent}"/>, and creates one subscription rule per handled type — a service receives only
    /// what it handles.
    /// <para>
    /// To change the endpoint shape, call <see cref="MessageTopologyServiceCollectionExtensions.AddMessageTopology"/>
    /// with its options before this; the two calls share one consumed-type set, and only the names this method
    /// back-fills from <see cref="AzureServiceBusOptions"/> are left to it.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Service Bus options (connection string, topic, subscription, sessions, provisioning).</param>
    /// <param name="handlerAssemblies">Assemblies to scan for handlers; defaults to the calling assembly.</param>
    public static IServiceCollection AddAzureServiceBusEventBus(
        this IServiceCollection services,
        Action<AzureServiceBusOptions> configure,
        params Assembly[] handlerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<AzureServiceBusOptions>().Configure(configure);

        var assemblies = handlerAssemblies is { Length: > 0 } ? handlerAssemblies : [Assembly.GetCallingAssembly()];
        services.AddEventHandlersFromAssemblies(assemblies);
        services.AddEventResilienceDefaults();

        // After the handler scan, never before: the consumed-type set that becomes the subscription rules is read out of
        // the registered IEventHandler<> descriptors, and a type registered later gets no rule.
        services.AddMessageTopology();

        // The shared endpoint's name has always been configured on the adapter's own options, so keep it there.
        // Null-coalesce rather than assign — an explicit AddMessageTopology(...) still wins.
        //
        // No dead-letter name is back-filled: the DLQ is the entity's own $DeadLetterQueue sub-queue, not a second
        // subscription this adapter names or provisions.
        services.AddOptions<TopologyOptions>().PostConfigure<IOptions<AzureServiceBusOptions>>((topology, serviceBus) =>
        {
            topology.SharedEndpointName ??= serviceBus.Value.Subscription;
        });

        services.TryAddSingleton<AzureServiceBusConnection>();
        services.TryAddSingleton<AzureServiceBusTopology>();
        services.TryAddSingleton<ITransportCapabilities, AzureServiceBusCapabilities>();
        services.TryAddSingleton<ISendTransport, AzureServiceBusSendTransport>();
        services.TryAddSingleton<IReceiveTransport, AzureServiceBusReceiveTransport>();
        services.TryAddSingleton<IEventBus, TransportEventBus>();
        services.TryAddSingleton<EventProcessingPipeline>();
        services.AddHostedService<TransportConsumerHostedService>();
        return services;
    }
}
