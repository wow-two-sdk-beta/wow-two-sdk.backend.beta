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

/// <summary>Stream-entry wire format — envelope to field/value pairs on send, and back on receive.</summary>
internal static class RedisStreamsWireFormatMapper
{
    /// <summary>The field/value pairs for one outgoing envelope.</summary>
    public static NameValueEntry[] BuildEntry(EventEnvelopeModel envelope, byte[] body, string typeToken, string contentType)
    {
        // Caller headers first, minus the fields re-derived below, then the adapter's own control fields.
        var fields = new List<NameValueEntry>(envelope.Headers.Count + 8);
        foreach (var (key, value) in envelope.Headers)
            if (!RedisStreamsFieldConstants.IsAdapterOwned(key))
                fields.Add(new NameValueEntry(key, value));

        fields.Add(new NameValueEntry(RedisStreamsFieldConstants.EventType, typeToken));
        fields.Add(new NameValueEntry(RedisStreamsFieldConstants.ContentType, contentType));
        fields.Add(new NameValueEntry(RedisStreamsFieldConstants.MessageId, envelope.MessageId));

        if (!string.IsNullOrEmpty(envelope.PartitionKey))
            fields.Add(new NameValueEntry(RedisStreamsFieldConstants.PartitionKey, envelope.PartitionKey));

        // Reply-address, correlation and conversation ride reserved fields, stamped only when set.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            fields.Add(new NameValueEntry(MessageHeaderConstants.ReplyTo, envelope.ReplyTo));
        if (!string.IsNullOrEmpty(envelope.CorrelationId))
            fields.Add(new NameValueEntry(RedisStreamsFieldConstants.CorrelationId, envelope.CorrelationId));
        if (!string.IsNullOrEmpty(envelope.ConversationId))
            fields.Add(new NameValueEntry(MessageHeaderConstants.ConversationId, envelope.ConversationId));

        // Last, so an operator running XRANGE sees the metadata before a wall of serialized body.
        fields.Add(new NameValueEntry(RedisStreamsFieldConstants.Body, body));
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
            if (name.Length == 0 || string.Equals(name, RedisStreamsFieldConstants.Body, StringComparison.Ordinal))
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

        // Scan backwards for the last non-null body field — a duplicate would otherwise shadow the real value.
        for (var index = values.Length - 1; index >= 0; index--)
        {
            var field = values[index];
            if (!field.Value.IsNull && string.Equals(field.Name.ToString(), RedisStreamsFieldConstants.Body, StringComparison.Ordinal))
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
                // Strip a prior death field so a replayed dead letter cannot report a stale reason.
                var name = field.Name.ToString();
                if (RedisStreamsFieldConstants.IsDeathField(name))
                    continue;

                fields.Add(field);
            }
        }

        fields.Add(new NameValueEntry(RedisStreamsFieldConstants.DeadLetterReason, reason));
        fields.Add(new NameValueEntry(RedisStreamsFieldConstants.DeadLetterSourceStream, sourceStream));
        fields.Add(new NameValueEntry(RedisStreamsFieldConstants.DeadLetterDeliveryCount, deliveryCount.ToString(CultureInfo.InvariantCulture)));
        if (exception is not null)
            fields.Add(new NameValueEntry(RedisStreamsFieldConstants.DeadLetterExceptionType, exception.GetType().FullName ?? exception.GetType().Name));

        return [.. fields];
    }
}
