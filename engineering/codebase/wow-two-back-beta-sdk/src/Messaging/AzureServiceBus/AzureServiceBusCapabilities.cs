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

namespace WoW.Two.Sdk.Backend.Beta.Messaging.AzureServiceBus;

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
    ///   - a reply address must name a provisioned entity — there is no zero-provisioning pseudo-queue
    ///   - <c>IRequestClient</c> replies to an ordinary consumed endpoint, which satisfies that
    /// </remarks>
    public bool NativeRequestReply => true;

    /// <summary>
    /// Settlement goes through <see cref="ServiceBusReceiver"/>, which holds the message lock for the entity's
    /// configured duration no matter which thread settles it — so a pump worker may complete or dead-letter a message
    /// the consume loop received.
    /// </summary>
    /// <remarks>
    ///   - false under <see cref="AzureServiceBusOptions.RequiresSession"/>
    ///   - a session's receiver is disposed once the session drains, so a later settle fails and the message redelivers
    ///   - a non-session receiver lives until <c>StopAsync</c>, which runs only after the drain
    /// </remarks>
    public bool SettlesInContext => !options.Value.RequiresSession;

    /// <summary>The receive loop is an ordinary async batch pull with no thread affinity, so the pump may dispatch in parallel.</summary>
    public bool ThreadAffineConsume => false;
}
