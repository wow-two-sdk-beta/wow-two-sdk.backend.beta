using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Intercepts a reply before dispatch: a message that answers a pending request completes it and stops there, so the
/// requesting process needs no handler for its own responses and the pipeline settles the reply normally.
/// </summary>
/// <remarks>Ordered like any other <see cref="IConsumeInterceptor"/> — first registered is outermost.</remarks>
internal sealed partial class ReplyingConsumeInterceptor(PendingRequestRegistry pending, ILogger<ReplyingConsumeInterceptor> logger) : IConsumeInterceptor
{
    public ValueTask InvokeAsync(ReceiveContext context, ConsumeDelegate next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var envelope = context.Envelope;

        // Short-circuit — skips resilience, dedupe and dispatch; the pipeline acknowledges as for any completed chain.
        if (pending.TryComplete(envelope))
        {
            LogResponseMatched(envelope.MessageId, envelope.ConversationId);
            return ValueTask.CompletedTask;
        }

        return next(context, cancellationToken);
    }

    [LoggerMessage(EventId = 6061, Level = LogLevel.Debug, Message = "Matched response message {MessageId} to pending request {ConversationId}")]
    private partial void LogResponseMatched(string messageId, string? conversationId);
}
