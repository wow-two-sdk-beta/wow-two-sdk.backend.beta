using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Messaging.Transport;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Ef;

/// <summary>
/// Rebuilds the publish-time context a row carries in <c>headers_json</c>: the staged header set, the
/// <see cref="PublishOptions"/> to dispatch it with, and the W3C trace-context continuation that keeps the outbox hop
/// inside the producer's trace instead of starting an orphan one.
/// </summary>
/// <remarks>
///   - reserved (<see cref="MessageHeaderConstants.ReservedPrefix"/>) keys lift onto their typed <see cref="PublishOptions"/> field, never pass through raw
///   - a raw reserved header staged on a row never reaches the wire — the adapter re-stamps its own from the envelope
///   - <c>headers_json</c> reads via System.Text.Json
/// </remarks>
internal static class OutboxDispatchHeaderMapper
{
    /// <summary>
    /// Deserialize a row's <c>headers_json</c>. An absent/empty/JSON-<c>null</c> column means "no headers"; malformed
    /// JSON throws, so the row fails loudly (error retained, attempts bumped) rather than dispatching without its headers.
    /// </summary>
    /// <param name="headersJson">The row's <c>headers_json</c> value.</param>
    public static Dictionary<string, string> Read(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Build the publish options for a staged row — reserved control headers lifted onto their typed fields, everything
    /// else (W3C trace context, application metadata) passed through as headers.
    /// </summary>
    /// <param name="staged">Headers read back from the row.</param>
    /// <param name="rowId">The row id — the transport message id when the row stages no explicit one, so a redelivery after a crash reuses the id the consumer's inbox deduplicates on.</param>
    public static PublishOptions ToPublishOptions(IReadOnlyDictionary<string, string> staged, Guid rowId)
    {
        Dictionary<string, string>? headers = null;
        foreach (var (key, value) in staged)
        {
            // Skip the adapter-owned control namespace — only the four lifted below have a typed home.
            if (MessageHeaderConstants.IsReserved(key))
                continue;

            (headers ??= new Dictionary<string, string>(StringComparer.Ordinal))[key] = value;
        }

        return new PublishOptions
        {
            MessageId = Lift(staged, MessageHeaderConstants.MessageId) ?? rowId.ToString("N"),
            ReplyTo = Lift(staged, MessageHeaderConstants.ReplyTo),
            ConversationId = Lift(staged, MessageHeaderConstants.ConversationId),
            PartitionKey = Lift(staged, MessageHeaderConstants.PartitionKey),
            Headers = headers,
        };
    }

    /// <summary>
    /// Start an activity parented to the row's staged W3C trace context, so the publish it wraps lands in the producer's
    /// trace. Null when the row carries no parsable <c>traceparent</c> (nothing to continue) or nothing is listening to
    /// the <see cref="ActivitySource"/> — in which case the raw header passed through in
    /// <see cref="ToPublishOptions"/> reaches the wire untouched and carries the trace by itself.
    /// </summary>
    /// <param name="staged">Headers read back from the row.</param>
    public static Activity? StartTraceContinuation(IReadOnlyDictionary<string, string> staged)
    {
        if (!staged.TryGetValue(MessageHeaderConstants.TraceParent, out var traceParent))
            return null;

        staged.TryGetValue(MessageHeaderConstants.TraceState, out var traceState);
        return ActivityContext.TryParse(traceParent, traceState, out var parent)
            ? MessagingDiagnosticConstants.Source.StartActivity("outbox dispatch", ActivityKind.Internal, parent)
            : null;
    }

    private static string? Lift(IReadOnlyDictionary<string, string> staged, string header)
        => staged.TryGetValue(header, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}
