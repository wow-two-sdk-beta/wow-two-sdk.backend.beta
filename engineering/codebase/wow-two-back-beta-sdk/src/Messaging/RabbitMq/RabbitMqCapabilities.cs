using System.Globalization;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RabbitMq;

/// <summary>
/// RabbitMQ capability flags — native dead-letter (DLX), per-message priority + TTL, publisher confirms and
/// direct-reply-to; no native delay/dedupe, no cross-queue ordering guarantee, no sessions or exactly-once transactions;
/// settlement is by delivery tag off the consume thread, so the concurrency pump may dispatch in parallel.
/// </summary>
internal sealed class RabbitMqCapabilities(IOptions<RabbitMqOptions> options) : ITransportCapabilities
{
    public bool NativeDeadLetter => true;

    public bool NativeDelay => false;

    public bool NativeDedupe => false;

    public bool NativeOrdering => false;

    /// <summary>
    /// Core AMQP: the <c>priority</c> message property, no plugin. Ranking is opt-in per queue — only a queue declared
    /// with the <c>x-max-priority</c> argument sorts by it; elsewhere the broker accepts the property and ignores it.
    /// So this tracks <see cref="RabbitMqOptions.MaxPriority"/> rather than reporting a flat true: without it the hint
    /// rides the wire and nothing ranks, and a pipeline reading this flag to choose native-versus-emulated would pick
    /// native on the strength of a property the broker discards.
    /// </summary>
    public bool NativePriority => options.Value.MaxPriority is not null;

    /// <summary>Core AMQP: the per-message <c>expiration</c> property (milliseconds). Independent of any queue-level <c>x-message-ttl</c>; when both are set the lower wins.</summary>
    public bool NativeTimeToLive => true;

    /// <summary>
    /// No absolute-time enqueue. The nearest facility is the delayed-message-exchange plugin, which is (a) a plugin,
    /// not core, and (b) a relative <c>x-delay</c> in milliseconds rather than a wall-clock target.
    /// </summary>
    public bool NativeScheduling => false;

    /// <summary>
    /// No session / message-group concept. Single-active-consumer locks an entire queue to one consumer rather than a
    /// group within it, and the consistent-hash exchange (a plugin) routes by key without an exclusive group lock.
    /// </summary>
    public bool NativeSessions => false;

    /// <summary>
    /// Core AMQP publisher confirms, and the adapter uses them: the publish channel is opened in confirm mode with
    /// tracking, so a send completes only once the broker has acked it and a nack surfaces as an exception.
    /// </summary>
    public bool NativePublisherConfirms => true;

    /// <summary>
    /// AMQP <c>tx.select</c>/<c>tx.commit</c> gives atomic batch publish only — no idempotent producer, no dedupe across
    /// a retry, and no consume-transform-produce atomicity — so it does not meet the exactly-once bar this flag sets.
    /// It is also mutually exclusive with publisher confirms on a channel.
    /// </summary>
    public bool NativeTransactions => false;

    /// <summary>
    /// Core AMQP: a native <c>reply-to</c> property on every message, which the adapter maps from
    /// <see cref="EventEnvelope.ReplyTo"/> in both directions, plus the broker's own direct-reply-to over the
    /// <c>amq.rabbitmq.reply-to</c> pseudo-queue.
    /// </summary>
    /// <remarks>Never consumes the <c>amq.rabbitmq.reply-to</c> pseudo-queue — <c>IRequestClient</c> replies to an ordinary bound endpoint.</remarks>
    public bool NativeRequestReply => true;

    /// <summary>RabbitMQ settles by delivery tag on the channel, which is safe from a pump worker thread.</summary>
    public bool SettlesInContext => true;

    /// <summary>Consumer callbacks are dispatched by the client, not owned by a caller-managed loop.</summary>
    public bool ThreadAffineConsume => false;
}
