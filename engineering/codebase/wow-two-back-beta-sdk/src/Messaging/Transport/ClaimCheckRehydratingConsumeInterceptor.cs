using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Messaging.Models;
using WoW.Two.Sdk.Backend.Beta.Messaging.Serialization.Serializers;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// The consume half: fetches an offloaded body back out of blob storage and swaps it into the envelope, so a handler is
/// dispatched the real event and never learns the body travelled separately.
/// </summary>
/// <remarks>
///   - the processing pipeline places this last, so <see cref="ReceiveContext.DeadLetterAsync"/> re-publishes the reference envelope
///   - a body that cannot be read back throws <see cref="ClaimCheckPayloadException"/>, so the message dead-letters with that reason
///   - to spend no retry budget on it, add <c>AddEventFaultClassification(r =&gt; r.DeadLetterOn&lt;ClaimCheckPayloadException&gt;())</c>
/// </remarks>
internal sealed partial class ClaimCheckRehydratingConsumeInterceptor(
    ClaimCheckPayloadRepository store,
    IMessageSerializer serializer,
    IMessageTypeMapper typeResolver,
    ILogger<ClaimCheckRehydratingConsumeInterceptor> logger) : IConsumeInterceptor
{
    public async ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var envelope = context.Envelope;

        // The whole cost of the feature on a normal message: one lookup for a header that is not there.
        if (!ClaimCheckHeaderConstants.TryReadReference(envelope, out var path))
        {
            await next(context, cancellationToken);
            return;
        }

        var bodyType = ResolveBodyType(envelope);
        // The pump reads only exceptions, so a failure crosses back to a throw at this seam, which knows the transport.
        var payload = (await store.ReadAsync(path, envelope.MessageId, cancellationToken))
            .Match(body => body, error => throw new ClaimCheckPayloadException(error.Message));

        // Name the claim check, so the dead-letter reason points at the payload and not at an opaque decoder error.
        if (serializer.Deserialize(payload, bodyType).IsFailure(out var decodeError, out var body))
        {
            throw new ClaimCheckPayloadException(
                $"Claim-checked body '{path}' for message '{envelope.MessageId}' could not be deserialized as {bodyType.Name}.",
                decodeError.ToException());
        }

        LogRehydrated(envelope.MessageId, payload.LongLength, path);

        // Settlement stays on the transport's own context, so it still describes the message that arrived.
        await next(new RehydratedReceiveContext(context, envelope with { Body = body, BodyType = bodyType }), cancellationToken);
    }

    // The header is a fallback — a token this process cannot resolve is ignored, the envelope already holds a type.
    private Type ResolveBodyType(EventEnvelopeModel envelope)
    {
        if (envelope.Headers.TryGetValue(ClaimCheckHeaderConstants.BodyType, out var token)
            && !string.IsNullOrWhiteSpace(token)
            && typeResolver.ResolveType(token) is { } resolved)
        {
            return resolved;
        }

        return envelope.BodyType;
    }

    [LoggerMessage(EventId = 6082, Level = LogLevel.Debug, Message = "Rehydrated the {SizeBytes}-byte body of message {MessageId} from claim check {Path}")]
    private partial void LogRehydrated(string messageId, long sizeBytes, string path);

    /// <summary>The received message with its body put back — settlement delegates to the transport's own context.</summary>
    private sealed class RehydratedReceiveContext(ReceiveContext inner, EventEnvelopeModel rehydrated) : ReceiveContext
    {
        public override EventEnvelopeModel Envelope => rehydrated;

        public override ValueTask AcknowledgeAsync(CancellationToken cancellationToken)
            => inner.AcknowledgeAsync(cancellationToken);

        public override ValueTask DeadLetterAsync(string reason, Exception? exception, CancellationToken cancellationToken)
            => inner.DeadLetterAsync(reason, exception, cancellationToken);
    }
}
