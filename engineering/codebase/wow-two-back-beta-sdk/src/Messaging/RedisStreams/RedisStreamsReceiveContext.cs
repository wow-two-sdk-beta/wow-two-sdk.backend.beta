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
        // XACK only clears the PEL — the entry stays in the log for other groups, and MAXLEN bounds the key.
        await database.StreamAcknowledgeAsync(sourceStream, options.ConsumerGroup, entry.Id);
    }

    public override async ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
    {
        await database.StreamAddAsync(
            options.DeadLetterStream,
            RedisStreamsWireFormatMapper.BuildDeadLetterEntry(entry, sourceStream, envelope.DeliveryCount, reason, exception),
            messageId: null,
            maxLength: options.DeadLetterMaxLength,
            useApproximateMaxLength: options.UseApproximateMaxLength);

        // Ack only after the DLQ write lands — at worst a later claim duplicates it into the DLQ.
        await database.StreamAcknowledgeAsync(sourceStream, options.ConsumerGroup, entry.Id);
    }
}
