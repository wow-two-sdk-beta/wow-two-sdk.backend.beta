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
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus;

/// <summary>Holds options for the Azure Service Bus event-bus adapter.</summary>
public sealed record AzureServiceBusOptions
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
    /// <see cref="IEndpointNameMapper"/>.
    /// </summary>
    public string Subscription { get; set; } = "wt-events";

    /// <summary>
    /// Create the topic, this process's subscriptions and their filter rules on start if absent. Default true.
    /// </summary>
    /// <remarks>
    ///   - turn it off when entities are provisioned out-of-band (Bicep/Terraform)
    ///   - provisioning needs <c>Manage</c> rights, so a Send/Listen-only namespace fails on the first management call
    /// </remarks>
    public bool ProvisionEntities { get; set; } = true;

    /// <summary>
    /// Broker-side delivery cap before Service Bus moves the message to the native dead-letter queue. Default 10.
    /// Applied only to a subscription this adapter creates — Service Bus ignores it on an existing one.
    /// </summary>
    /// <remarks>
    ///   - the crash safety-net, never the retry policy
    ///   - in-process retries are the SDK resilience pipeline's
    ///   - reached only when the process dies mid-handler and the lock expires
    /// </remarks>
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
    /// </summary>
    /// <remarks>
    ///   - a prefetched message's lock clock runs while it waits in the local buffer
    ///   - a prefetch larger than the pipeline drains within <see cref="LockDuration"/> expires locks and redelivers
    /// </remarks>
    public int PrefetchCount { get; set; }

    /// <summary>Messages requested per receive call. Default 10. A batch is dispatched one message at a time, so this is a network-round-trip knob, not a concurrency one — concurrency is <see cref="ConcurrencyOptions.MaxConcurrentMessages"/>.</summary>
    public int MaxMessagesPerReceive { get; set; } = 10;

    /// <summary>How long a receive call waits for the first message before returning empty and looping. Default 30s. Longer means fewer idle round-trips; it does not delay a message that arrives during the wait.</summary>
    public TimeSpan ReceiveWaitTime { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Consume through sessions: subscriptions this adapter creates are session-enabled, the send path maps
    /// <see cref="EventEnvelopeModel.PartitionKey"/> to <c>SessionId</c>, and the receive path locks one session at a time so
    /// a key's messages are processed in order by a single consumer. Default false.
    /// </summary>
    /// <remarks>
    ///   - fixed when a subscription is created, so turning it on later needs a new subscription name
    ///   - makes <see cref="ITransportCapabilities.NativeSessions"/> and <see cref="ITransportCapabilities.NativeOrdering"/> report true
    ///   - both capabilities belong to a session-enabled entity, never to the namespace
    /// </remarks>
    public bool RequiresSession { get; set; }

    /// <summary>Sessions locked concurrently per subscription when <see cref="RequiresSession"/> is on. Default 1. Each is an independent accept loop, so this is how a session-ordered deployment scales out across keys.</summary>
    public int MaxConcurrentSessions { get; set; } = 1;

    /// <summary>How long a locked session may sit empty before it is released and the loop accepts the next one. Default 30s. Too short churns session locks; too long starves other sessions of a scarce accept loop.</summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Create the topic with duplicate detection, so Service Bus drops a re-send carrying a
    /// <see cref="EventEnvelopeModel.MessageId"/> it has already seen within <see cref="DuplicateDetectionWindow"/>.
    /// Default false; this is what <see cref="ITransportCapabilities.NativeDedupe"/> tracks.
    /// </summary>
    /// <remarks>
    ///   - fixed at topic creation
    ///   - dedupes on the produce side, which is what an ambiguous send retry needs
    ///   - the SDK inbox dedupes on the consume side regardless
    /// </remarks>
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
