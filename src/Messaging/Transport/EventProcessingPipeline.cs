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
/// Registered <see cref="IReceiveObserver"/>s watch the whole message, <see cref="IConsumeObserver"/>s each delivery
/// attempt; unlike a filter, neither can short-circuit the chain or change settlement.
/// </summary>
internal sealed partial class EventProcessingPipeline(
    IServiceScopeFactory scopeFactory,
    EventDispatcherRegistry registry,
    IEventBus bus,
    IEventResiliencePipeline resilience,
    IEnumerable<IConsumeFilter> filters,
    IEnumerable<IReceiveObserver> receiveObservers,
    IEnumerable<IConsumeObserver> consumeObservers,
    IMessagingMetrics metrics,
    ILogger<EventProcessingPipeline> logger)
{
    // Materialized once: the hot path only pays a length check when nothing is observing.
    private readonly IReceiveObserver[] _receiveObservers = [.. receiveObservers];
    private readonly IConsumeObserver[] _consumeObservers = [.. consumeObservers];

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

        // The success / duplicate / no-handler outcomes are counted inside the core, which is the only place that knows
        // which one happened; the faulted outcome is counted here, where nothing reached the core's record point.
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await _receiveObservers.NotifyPreReceiveAsync(context, logger, cancellationToken);
            await (_chain ??= BuildChain())(context, cancellationToken);
            await context.AcknowledgeAsync(cancellationToken);
            await _receiveObservers.NotifyPostReceiveAsync(context, logger, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogProcessingFailed(ex, envelope.MessageId);
            metrics.RecordConsumed(envelope.Destination, envelope.BodyType, ConsumeOutcome.Faulted);
            metrics.RecordDeadLettered(envelope.Destination, envelope.BodyType, ex);
            await context.DeadLetterAsync(ex.Message, ex, cancellationToken);
            await _receiveObservers.NotifyReceiveFaultAsync(context, ex, logger, cancellationToken); // after settlement — the message is already at rest
        }
        finally
        {
            metrics.RecordConsumeDuration(envelope.Destination, envelope.BodyType, Stopwatch.GetElapsedTime(startedAt));
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
        var attempt = 0; // attempts run sequentially inside ExecuteAsync, so a plain counter is enough
        await resilience.ExecuteAsync(async attemptToken =>
        {
            if (++attempt > 1)
                metrics.RecordRetried(envelope.Destination, envelope.BodyType);

            await _consumeObservers.NotifyPreConsumeAsync(context, logger, attemptToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IInboxProcessor>();
                var dispatched = false;
                var processed = await inbox.ProcessOnceAsync(envelope.MessageId, async handlerToken =>
                {
                    if (registry.TryGet(envelope.BodyType, out var dispatcher))
                    {
                        await dispatcher.DispatchAsync(scope.ServiceProvider, envelope, bus, handlerToken);
                        dispatched = true;
                    }
                    else
                    {
                        LogNoHandler(envelope.BodyType.FullName);
                    }
                }, attemptToken);

                // Only reached when the attempt did not throw, so each message counts exactly once: a failing attempt
                // either retries (and records here on the attempt that finally lands) or escapes to the faulted count.
                ConsumeOutcome outcome;
                if (!processed)
                {
                    LogDuplicateSkipped(envelope.MessageId);
                    outcome = ConsumeOutcome.Duplicate;
                    metrics.RecordConsumed(envelope.Destination, envelope.BodyType, ConsumeOutcome.Duplicate);
                }
                else
                {
                    outcome = dispatched ? ConsumeOutcome.Success : ConsumeOutcome.NoHandler;
                    metrics.RecordConsumed(envelope.Destination, envelope.BodyType, outcome);
                }

                await _consumeObservers.NotifyPostConsumeAsync(context, outcome, logger, attemptToken);
            }
            // The filter keeps the no-observer path identical: with nothing registered the catch is never entered and the fault propagates untouched into the retry decision.
            catch (Exception ex) when (_consumeObservers.Length != 0 && !MessageObserverNotifications.IsCancellation(ex, attemptToken))
            {
                await _consumeObservers.NotifyConsumeFaultAsync(context, ex, logger, attemptToken);
                throw;
            }
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
