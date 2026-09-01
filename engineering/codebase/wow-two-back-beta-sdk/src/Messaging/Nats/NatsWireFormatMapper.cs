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
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Nats;

/// <summary>Shared JetStream envelope wire-format helpers (serialize body + headers; reconstruct on receive).</summary>
internal static class NatsWireFormatMapper
{
    public static NatsHeaders BuildHeaders(EventEnvelope envelope, string typeToken, string contentType)
    {
        // Caller headers first, minus the adapter-owned wt-* namespace — a forwarded key would misroute the new body.
        var headers = new NatsHeaders();
        foreach (var (key, value) in envelope.Headers)
            if (!MessageHeaderConstants.IsAdapterOwned(key))
                headers[key] = value;

        headers[MessageHeaderConstants.EventType] = typeToken;
        headers[MessageHeaderConstants.ContentType] = contentType;
        headers[MessageHeaderConstants.MessageId] = envelope.MessageId;
        if (!string.IsNullOrEmpty(envelope.PartitionKey))
            headers[MessageHeaderConstants.PartitionKey] = envelope.PartitionKey;

        // A JetStream publish has no reply field, so reply, correlation and conversation ride the reserved headers.
        if (!string.IsNullOrEmpty(envelope.ReplyTo))
            headers[MessageHeaderConstants.ReplyTo] = envelope.ReplyTo;
        if (!string.IsNullOrEmpty(envelope.CorrelationId))
            headers[MessageHeaderConstants.CorrelationId] = envelope.CorrelationId;
        if (!string.IsNullOrEmpty(envelope.ConversationId))
            headers[MessageHeaderConstants.ConversationId] = envelope.ConversationId;

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
        headers[MessageHeaderConstants.DeadLetterReason] = reason;
        if (exception is not null)
            headers[MessageHeaderConstants.DeadLetterExceptionType] = exception.GetType().FullName ?? exception.GetType().Name;
        return headers;
    }
}
