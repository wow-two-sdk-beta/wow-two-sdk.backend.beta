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
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.RedisStreams;

/// <summary>Well-known stream-entry field names carrying SDK envelope metadata. A stream entry is a flat field/value map, so each header is its own field alongside the body.</summary>
/// <remarks>
///   - every cross-broker name aliases <see cref="MessageHeaderConstants"/>, never a literal — one constant per key, all adapters
///   - only the keys Redis alone needs are declared here: the body field and the dead-letter provenance fields
/// </remarks>
internal static class RedisStreamsFieldConstants
{
    /// <summary>
    /// Holds the serialized body. Reserved, and stripped on send by <see cref="IsAdapterOwned"/>, so the headers SDK
    /// features stamp still reach the wire.
    /// </summary>
    public const string Body = MessageHeaderConstants.ReservedPrefix + "body";

    /// <summary>Holds carries the event's stable type token so the consumer can resolve the CLR type.</summary>
    public const string EventType = MessageHeaderConstants.EventType;

    /// <summary>Holds carries the serializer content type, so the consumer selects the matching deserializer.</summary>
    public const string ContentType = MessageHeaderConstants.ContentType;

    /// <summary>Holds carries <see cref="EventEnvelopeModel.MessageId"/> — Redis assigns its own entry id, so the SDK's identity needs a field of its own.</summary>
    public const string MessageId = MessageHeaderConstants.MessageId;

    /// <summary>Holds carries the envelope's ordering / partition key so the consumer can preserve per-key ordering.</summary>
    public const string PartitionKey = MessageHeaderConstants.PartitionKey;

    /// <summary>Holds carries <see cref="EventEnvelopeModel.CorrelationId"/>. Redis has no correlation property, so it rides a field.</summary>
    public const string CorrelationId = MessageHeaderConstants.CorrelationId;

    /// <summary>Holds why the entry was dead-lettered, stamped on the copy written to the dead-letter stream.</summary>
    public const string DeadLetterReason = MessageHeaderConstants.DeadLetterReason;

    /// <summary>Holds type name of the terminal exception, stamped alongside <see cref="DeadLetterReason"/>.</summary>
    public const string DeadLetterExceptionType = MessageHeaderConstants.DeadLetterExceptionType;

    /// <summary>Holds stream the entry died on. The DLQ is one key for every routed stream, so without this a dead letter cannot be traced back to its source.</summary>
    public const string DeadLetterSourceStream = MessageHeaderConstants.ReservedPrefix + "dl-source-stream";

    /// <summary>Holds pEL delivery count at the moment of death — how many attempts the message actually consumed.</summary>
    public const string DeadLetterDeliveryCount = MessageHeaderConstants.ReservedPrefix + "dl-delivery-count";

    /// <summary>
    /// True when this adapter re-derives <paramref name="name"/> on every write, so a caller-supplied copy must be
    /// dropped instead of riding the wire.
    /// </summary>
    /// <remarks>
    ///   - a Redis-scoped superset of <see cref="MessageHeaderConstants.IsAdapterOwned"/>, adding the body and the death fields
    ///   - a stream entry is a field list, not a map, so an unstripped copy rides beside the genuine value
    ///   - scan direction decides which of the two a reader sees
    /// </remarks>
    /// <param name="name">The field name.</param>
    public static bool IsAdapterOwned(string name)
        => MessageHeaderConstants.IsAdapterOwned(name) || name is Body || IsDeathField(name);

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
