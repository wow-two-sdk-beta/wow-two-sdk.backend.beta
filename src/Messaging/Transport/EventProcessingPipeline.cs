using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Transport-agnostic processing of one received message: start a CONSUMER span (extracting trace context), run the
/// ordered <see cref="IConsumeFilter"/> chain around the core (resilience → dedupe via <see cref="IInboxProcessor"/> →
/// dispatch), then settle via the <see cref="ReceiveContext"/> — acknowledge on success, dead-letter on exhaustion.
/// Shared by every receive transport (in-memory, RabbitMQ, …). With no filters registered the chain is the bare core.
/// </summary>
internal sealed partial class EventProcessingPipeline(
    IServiceScopeFactory scopeFactory,
    EventDispatcherRegistry registry,
    IEventBus bus,
    IEventResiliencePipeline resilience,
    IEnumerable<IConsumeFilter> filters,
    ILogger<EventProcessingPipeline> logger)
{
    private ConsumeDelegate? _chain;

    public async ValueTask ProcessAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var envelope = context.Envelope;

        using var activity = MessagingDiagnostics.Source.StartActivity(envelope.Destination, ActivityKind.Consumer, ExtractParentContext(envelope.Headers));
        if (activity is not null)
        {
            activity.SetTag("messaging.operation.name", "process");
            activity.SetTag("messaging.destination.name", envelope.Destination);
            activity.SetTag("messaging.message.id", envelope.MessageId);
        }

        try
        {
            await (_chain ??= BuildChain())(context, cancellationToken);
            await context.AcknowledgeAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogProcessingFailed(ex, envelope.MessageId);
            await context.DeadLetterAsync(ex.Message, ex, cancellationToken);
        }
    }

    // Build the ordered filter chain once: filters wrap the core (first-registered = outermost), core is innermost.
    private ConsumeDelegate BuildChain()
    {
        ConsumeDelegate chain = CoreConsumeAsync;
        foreach (var filter in filters.Reverse())
        {
            var next = chain;
            chain = (ctx, token) => filter.InvokeAsync(ctx, next, token);
        }

        return chain;
    }

    // The retry/dedupe/dispatch core: resilience wraps a per-attempt scope whose inbox dedupes and runs the dispatch.
    private async ValueTask CoreConsumeAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        var envelope = context.Envelope;
        await resilience.ExecuteAsync(async attemptToken =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var inbox = scope.ServiceProvider.GetRequiredService<IInboxProcessor>();
            var processed = await inbox.ProcessOnceAsync(envelope.MessageId, async handlerToken =>
            {
                if (registry.TryGet(envelope.BodyType, out var dispatcher))
                    await dispatcher.DispatchAsync(scope.ServiceProvider, envelope, bus, handlerToken);
                else
                    LogNoHandler(envelope.BodyType.FullName);
            }, attemptToken);

            if (!processed)
                LogDuplicateSkipped(envelope.MessageId);
        }, cancellationToken);
    }

    private static ActivityContext ExtractParentContext(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue(MessagingDiagnostics.TraceParentHeader, out var traceParent))
        {
            headers.TryGetValue(MessagingDiagnostics.TraceStateHeader, out var traceState);
            if (ActivityContext.TryParse(traceParent, traceState, out var context))
                return context;
        }

        return default;
    }

    [LoggerMessage(EventId = 6011, Level = LogLevel.Debug, Message = "Skipping duplicate message {MessageId}")]
    private partial void LogDuplicateSkipped(string messageId);

    [LoggerMessage(EventId = 6012, Level = LogLevel.Warning, Message = "No handler registered for event type {EventType}")]
    private partial void LogNoHandler(string? eventType);

    [LoggerMessage(EventId = 6013, Level = LogLevel.Error, Message = "Processing exhausted for message {MessageId}; dead-lettering")]
    private partial void LogProcessingFailed(Exception exception, string messageId);
}
