using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability;
using WoW.Two.Sdk.Backend.Beta.Messaging.Reliability.Services;
using WoW.Two.Sdk.Backend.Beta.Messaging.Services;
using WoW.Two.Sdk.Backend.Beta.Messaging.Buses;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Transport-agnostic processing of one received message: start a CONSUMER span (extracting trace context), run the
/// ordered <see cref="IConsumeInterceptor"/> chain around the core (resilience → dedupe via <see cref="IInboxProcessor"/> →
/// dispatch), then settle via the <see cref="ReceiveContext"/> — acknowledge on success, dead-letter on exhaustion.
/// Shared by every receive transport (in-memory, RabbitMQ, …). With no filters registered the chain is the bare core.
/// Registered <see cref="IReceiveObservingInterceptor"/>s watch the whole message, <see cref="IConsumeObservingInterceptor"/>s each delivery
/// attempt; unlike a filter, neither can short-circuit the chain or change settlement.
/// Under <see cref="DelayedRetryOptions"/> a retryable failure is settled and re-published for a later delivery instead
/// of waiting inside the resilience pipeline, so the backoff no longer holds the consumer slot.
/// </summary>
internal sealed partial class EventProcessingPipeline(
    IServiceScopeFactory scopeFactory,
    EventDispatcherRegistry registry,
    IEventBus bus,
    IEventResiliencePipeline resilience,
    IEnumerable<IConsumeInterceptor> filters,
    IEnumerable<IReceiveObservingInterceptor> receiveObservers,
    IEnumerable<IConsumeObservingInterceptor> consumeObservers,
    IMessagingMetricsService metrics,
    ILogger<EventProcessingPipeline> logger,
    DelayedRetryService? delayedRetry = null)
{
    // Materialized once: the hot path only pays a length check when nothing is observing.
    private readonly IReceiveObservingInterceptor[] _receiveObservers = [.. receiveObservers];
    private readonly IConsumeObservingInterceptor[] _consumeObservers = [.. consumeObservers];
    private readonly IConsumeInterceptor[] _filters = OrderFilters(filters);

    private ConsumeDelegate? _chain;

    public async ValueTask ProcessAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var envelope = context.Envelope;

        using var activity = MessagingDiagnosticConstants.Source.StartActivity(envelope.Destination, ActivityKind.Consumer, ExtractParentContext(envelope.Headers));
        if (activity is not null)
        {
            activity.SetTag("messaging.operation.name", "process");
            activity.SetTag("messaging.destination.name", envelope.Destination);
            activity.SetTag("messaging.message.id", envelope.MessageId);
        }

        // Count a fault here because successful terminal outcomes are counted inside the core.
        var startedAt = Stopwatch.GetTimestamp();
        var chainCompleted = false;
        try
        {
            await _receiveObservers.NotifyPreReceiveAsync(context.Envelope, logger, cancellationToken);
            await (_chain ??= BuildChain())(context, cancellationToken);
            chainCompleted = true; // past this point a failure is settlement, not processing — the handler already ran
            await context.AcknowledgeAsync(cancellationToken);
            await _receiveObservers.NotifyPostReceiveAsync(context.Envelope, logger, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Acknowledge a successfully rescheduled handler fault without recording a terminal outcome.
            if (!chainCompleted && delayedRetry is not null && await delayedRetry.TryReEnqueueAsync(context, ex, cancellationToken))
            {
                await context.AcknowledgeAsync(cancellationToken);
                return;
            }

            LogProcessingFailed(ex, envelope.MessageId);
            metrics.RecordConsumed(envelope.Destination, envelope.BodyType, ConsumeOutcome.Faulted);
            metrics.RecordDeadLettered(envelope.Destination, envelope.BodyType, ex);
            await context.DeadLetterAsync(ex.Message, ex, cancellationToken);
            await _receiveObservers.NotifyReceiveFaultAsync(context.Envelope, ex, logger, cancellationToken); // after settlement — the message is already at rest
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
        foreach (var filter in _filters.Reverse())
        {
            var next = chain;
            chain = (ctx, token) => filter.InvokeAsync(ctx, next, token);
        }

        return chain;
    }

    private static IConsumeInterceptor[] OrderFilters(IEnumerable<IConsumeInterceptor> filters)
    {
        var materialized = filters.ToList();
        var claimChecks = materialized
            .Where(static filter => filter is ClaimCheckRehydratingConsumeInterceptor)
            .ToArray();
        materialized.RemoveAll(static filter => filter is ClaimCheckRehydratingConsumeInterceptor);
        materialized.AddRange(claimChecks);

        return [.. materialized];
    }

    // The retry/dedupe/dispatch core: resilience wraps a per-attempt scope whose inbox dedupes and runs the dispatch.
    private async ValueTask CoreConsumeAsync(ReceiveContext context, CancellationToken cancellationToken)
    {
        var envelope = context.Envelope;
        var attempt = 0; // attempts run sequentially inside ExecuteAsync, so a plain counter is enough
        var outcomeRecorded = false; // did any attempt reach the record point below — see the Ignored count after the loop

        // Add delayed-retry attempts carried on the envelope to this delivery's local attempt count.
        var priorAttempts = delayedRetry is { IsActive: true } ? Math.Max(envelope.DeliveryCount, 0) : 0;
        await resilience.ExecuteAsync(async attemptToken =>
        {
            if (++attempt + priorAttempts > 1)
                metrics.RecordRetried(envelope.Destination, envelope.BodyType);

            await _consumeObservers.NotifyPreConsumeAsync(context.Envelope, logger, attemptToken);
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

                // Record one terminal outcome after the attempt completes without throwing.
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

                outcomeRecorded = true;
                await _consumeObservers.NotifyPostConsumeAsync(context.Envelope, outcome, logger, attemptToken);
            }
            // The filter keeps the no-observer path identical: with nothing registered the catch is never entered and the fault propagates untouched into the retry decision.
            catch (Exception ex) when (_consumeObservers.Length != 0 && !ObservingInterceptorExtensions.IsCancellation(ex, attemptToken))
            {
                await _consumeObservers.NotifyConsumeFaultAsync(context.Envelope, ex, logger, attemptToken);
                throw;
            }
        }, cancellationToken);

        // Record an ignored fault when no attempt reached a terminal outcome.
        if (!outcomeRecorded)
            metrics.RecordConsumed(envelope.Destination, envelope.BodyType, ConsumeOutcome.Ignored);
    }

    private static ActivityContext ExtractParentContext(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue(MessagingDiagnosticConstants.TraceParentHeader, out var traceParent))
        {
            headers.TryGetValue(MessagingDiagnosticConstants.TraceStateHeader, out var traceState);
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
