using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Watches the send path — notified around the hand-off of an <see cref="EventEnvelope"/> to the
/// <see cref="ISendTransport"/>. Register with <c>AddMessageObserver&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// An observer is <b>not</b> an <see cref="IConsumeFilter"/>. A filter sits <i>in</i> the call chain: it wraps the next
/// step and may short-circuit it, transform the context, or swallow a fault — it decides what happens. An observer only
/// watches: it is notified out of band, it is handed no continuation, and an exception it throws is caught and logged
/// rather than propagated. Nothing an observer does can change whether a message is sent, dispatched, retried,
/// acknowledged or dead-lettered. Use a filter to change the outcome, an observer to record it (audit store, test
/// harness, fault publishing, diagnostics). Observers are resolved once as singletons, so they must be thread-safe.
/// </remarks>
public interface IPublishObserver
{
    /// <summary>The envelope is fully built (ids, transport hints, trace-context headers) and is about to be handed to the transport.</summary>
    /// <param name="envelope">The envelope about to be sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PrePublishAsync(EventEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>The transport accepted the envelope. What that proves is transport-specific — see <see cref="ITransportCapabilities.NativePublisherConfirms"/>.</summary>
    /// <param name="envelope">The envelope that was sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PostPublishAsync(EventEnvelope envelope, CancellationToken cancellationToken);

    /// <summary>The transport threw while sending. The exception still propagates to the caller; this hook only records it. Not raised when the send is cancelled through the caller's own token.</summary>
    /// <param name="envelope">The envelope that failed to send.</param>
    /// <param name="exception">The fault thrown by the transport.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PublishFaultAsync(EventEnvelope envelope, Exception exception, CancellationToken cancellationToken);
}

/// <summary>
/// Watches the receive path — notified <b>once per received message</b>, spanning the whole of processing including
/// settlement. Register with <c>AddMessageObserver&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// An observer is <b>not</b> an <see cref="IConsumeFilter"/>. A filter wraps the next step and may short-circuit,
/// transform, or swallow a fault; an observer is notified out of band and cannot alter the outcome — an exception it
/// throws is caught and logged, never propagated. Use a filter to change what happens to the message, an observer to
/// record what happened. Pair with <see cref="IConsumeObserver"/> for per-attempt visibility: a receive is one message
/// end-to-end, a consume is one delivery attempt inside it. Observers are singletons and must be thread-safe.
/// </remarks>
public interface IReceiveObserver
{
    /// <summary>A message arrived and is about to enter the filter chain.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PreReceiveAsync(ReceiveContext context, CancellationToken cancellationToken);

    /// <summary>The message was processed and acknowledged — the terminal success hook.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PostReceiveAsync(ReceiveContext context, CancellationToken cancellationToken);

    /// <summary>Processing was exhausted and the message has been dead-lettered — the terminal failure hook, raised after settlement so the message is already at rest.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The terminal fault.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ReceiveFaultAsync(ReceiveContext context, Exception exception, CancellationToken cancellationToken);
}

/// <summary>
/// Watches dispatch — notified <b>once per delivery attempt</b>, inside the resilience loop, around dedupe and handler
/// dispatch. A retried message notifies these hooks again per attempt. Register with <c>AddMessageObserver&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// An observer is <b>not</b> an <see cref="IConsumeFilter"/>. A filter wraps the next step and may short-circuit,
/// transform, or swallow a fault; an observer is notified out of band and cannot alter the outcome — an exception it
/// throws is caught and logged, and <see cref="IConsumeObserver.ConsumeFaultAsync"/> in particular does not suppress the
/// fault it reports: the message still retries and dead-letters exactly as it would with no observer registered. Use a filter to
/// change what happens, an observer to record it. Observers are singletons and must be thread-safe.
/// </remarks>
public interface IConsumeObserver
{
    /// <summary>A delivery attempt is starting, before the inbox dedupe check and handler dispatch.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PreConsumeAsync(ReceiveContext context, CancellationToken cancellationToken);

    /// <summary>The attempt completed without throwing, with the outcome that was recorded to metrics.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="outcome">How the attempt ended — dispatched, skipped as a duplicate, or unhandled.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask PostConsumeAsync(ReceiveContext context, ConsumeOutcome outcome, CancellationToken cancellationToken);

    /// <summary>The attempt threw. The fault still propagates into the retry/dead-letter decision. Not raised when the attempt is cancelled through its own token.</summary>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The fault thrown by the attempt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ConsumeFaultAsync(ReceiveContext context, Exception exception, CancellationToken cancellationToken);
}

/// <summary>DI registration for message observers — the watch-only counterpart to <c>AddConsumeFilter&lt;T&gt;()</c>.</summary>
public static class MessageObserverServiceCollectionExtensions
{
    /// <summary>
    /// Register an observer of the message pipeline. <typeparamref name="TObserver"/> may implement any combination of
    /// <see cref="IPublishObserver"/>, <see cref="IReceiveObserver"/> and <see cref="IConsumeObserver"/>: it is added as
    /// a single singleton and surfaced under each interface it implements, so one instance sees every hook it declares.
    /// Calling this twice for the same type notifies that type twice.
    /// </summary>
    /// <typeparam name="TObserver">The observer implementation; must implement at least one observer interface.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <exception cref="InvalidOperationException"><typeparamref name="TObserver"/> is abstract, or implements no observer interface — either way the registration would fail or silently do nothing later, so it fails here instead.</exception>
    public static IServiceCollection AddMessageObserver<TObserver>(this IServiceCollection services)
        where TObserver : class
    {
        ArgumentNullException.ThrowIfNull(services);

        var observerType = typeof(TObserver);
        if (observerType.IsAbstract)
            throw new InvalidOperationException($"'{observerType}' is abstract or an interface — register a concrete observer type.");

        var observesPublish = typeof(IPublishObserver).IsAssignableFrom(observerType);
        var observesReceive = typeof(IReceiveObserver).IsAssignableFrom(observerType);
        var observesConsume = typeof(IConsumeObserver).IsAssignableFrom(observerType);

        if (!observesPublish && !observesReceive && !observesConsume)
            throw new InvalidOperationException($"'{observerType}' implements none of {nameof(IPublishObserver)}, {nameof(IReceiveObserver)}, {nameof(IConsumeObserver)} — there is nothing to observe.");

        // One concrete singleton, resolved through each facet, so an observer implementing several interfaces keeps one identity (and one piece of state).
        services.TryAddSingleton<TObserver>();

        if (observesPublish)
            services.AddSingleton(static provider => (IPublishObserver)provider.GetRequiredService<TObserver>());
        if (observesReceive)
            services.AddSingleton(static provider => (IReceiveObserver)provider.GetRequiredService<TObserver>());
        if (observesConsume)
            services.AddSingleton(static provider => (IConsumeObserver)provider.GetRequiredService<TObserver>());

        return services;
    }
}

/// <summary>
/// Fan-out helpers that notify the registered observers from the bus and the processing pipeline.
/// </summary>
/// <remarks>
/// Two invariants live here, and only here.
/// <list type="bullet">
/// <item><description>
/// <b>Isolation</b> — every hook is invoked inside its own <c>try</c>/<c>catch</c>, per observer. A throwing observer is
/// logged and the loop moves to the next one, so no observer can break message processing or change settlement.
/// </description></item>
/// <item><description>
/// <b>Zero cost when unused</b> — each entry point is a non-async method that returns <see cref="ValueTask.CompletedTask"/>
/// on an empty array, so the empty case costs one length check and never enters an async state machine or allocates.
/// The fault hooks are additionally guarded by an exception filter at the call site, so with no observers registered the
/// <c>catch</c> is not even entered and the exception propagates exactly as it did before.
/// </description></item>
/// </list>
/// </remarks>
internal static partial class MessageObserverNotifications
{
    /// <summary>True when the exception is co-operative cancellation of <paramref name="cancellationToken"/> rather than a real fault — shutdown is not something to report as a fault.</summary>
    /// <param name="exception">The exception under inspection.</param>
    /// <param name="cancellationToken">The token driving the operation.</param>
    public static bool IsCancellation(Exception exception, CancellationToken cancellationToken)
        => exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    /// <summary>Notify <see cref="IPublishObserver.PrePublishAsync"/>.</summary>
    /// <param name="observers">The registered publish observers.</param>
    /// <param name="envelope">The envelope about to be sent.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPrePublishAsync(this IPublishObserver[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PrePublishCoreAsync(observers, envelope, logger, cancellationToken);

    /// <summary>Notify <see cref="IPublishObserver.PostPublishAsync"/>.</summary>
    /// <param name="observers">The registered publish observers.</param>
    /// <param name="envelope">The envelope that was sent.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPostPublishAsync(this IPublishObserver[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PostPublishCoreAsync(observers, envelope, logger, cancellationToken);

    /// <summary>Notify <see cref="IPublishObserver.PublishFaultAsync"/>.</summary>
    /// <param name="observers">The registered publish observers.</param>
    /// <param name="envelope">The envelope that failed to send.</param>
    /// <param name="exception">The fault thrown by the transport.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPublishFaultAsync(this IPublishObserver[] observers, EventEnvelope envelope, Exception exception, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PublishFaultCoreAsync(observers, envelope, exception, logger, cancellationToken);

    /// <summary>Notify <see cref="IReceiveObserver.PreReceiveAsync"/>.</summary>
    /// <param name="observers">The registered receive observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPreReceiveAsync(this IReceiveObserver[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PreReceiveCoreAsync(observers, context, logger, cancellationToken);

    /// <summary>Notify <see cref="IReceiveObserver.PostReceiveAsync"/>.</summary>
    /// <param name="observers">The registered receive observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPostReceiveAsync(this IReceiveObserver[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PostReceiveCoreAsync(observers, context, logger, cancellationToken);

    /// <summary>Notify <see cref="IReceiveObserver.ReceiveFaultAsync"/>.</summary>
    /// <param name="observers">The registered receive observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The terminal fault.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyReceiveFaultAsync(this IReceiveObserver[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : ReceiveFaultCoreAsync(observers, context, exception, logger, cancellationToken);

    /// <summary>Notify <see cref="IConsumeObserver.PreConsumeAsync"/>.</summary>
    /// <param name="observers">The registered consume observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPreConsumeAsync(this IConsumeObserver[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PreConsumeCoreAsync(observers, context, logger, cancellationToken);

    /// <summary>Notify <see cref="IConsumeObserver.PostConsumeAsync"/>.</summary>
    /// <param name="observers">The registered consume observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="outcome">The outcome recorded for this attempt.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPostConsumeAsync(this IConsumeObserver[] observers, ReceiveContext context, ConsumeOutcome outcome, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PostConsumeCoreAsync(observers, context, outcome, logger, cancellationToken);

    /// <summary>Notify <see cref="IConsumeObserver.ConsumeFaultAsync"/>.</summary>
    /// <param name="observers">The registered consume observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The fault thrown by the attempt.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyConsumeFaultAsync(this IConsumeObserver[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : ConsumeFaultCoreAsync(observers, context, exception, logger, cancellationToken);

    private static async ValueTask PrePublishCoreAsync(IPublishObserver[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PrePublishAsync(envelope, cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolation: a faulted observer is recorded and skipped — it must never fail the publish.
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IPublishObserver.PrePublishAsync), envelope.MessageId);
            }
        }
    }

    private static async ValueTask PostPublishCoreAsync(IPublishObserver[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PostPublishAsync(envelope, cancellationToken);
            }
            catch (Exception ex)
            {
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IPublishObserver.PostPublishAsync), envelope.MessageId);
            }
        }
    }

    private static async ValueTask PublishFaultCoreAsync(IPublishObserver[] observers, EventEnvelope envelope, Exception exception, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PublishFaultAsync(envelope, exception, cancellationToken);
            }
            catch (Exception ex)
            {
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IPublishObserver.PublishFaultAsync), envelope.MessageId);
            }
        }
    }

    private static async ValueTask PreReceiveCoreAsync(IReceiveObserver[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PreReceiveAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolation: the message goes on to the filter chain regardless of what an observer did here.
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IReceiveObserver.PreReceiveAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask PostReceiveCoreAsync(IReceiveObserver[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PostReceiveAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolation: the message is already acknowledged; a faulted observer must not turn a settled success into a dead-letter.
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IReceiveObserver.PostReceiveAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask ReceiveFaultCoreAsync(IReceiveObserver[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.ReceiveFaultAsync(context, exception, cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolation: never let a fault-handling observer mask the fault it was told about.
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IReceiveObserver.ReceiveFaultAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask PreConsumeCoreAsync(IConsumeObserver[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PreConsumeAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolation: a faulted observer must not become a delivery attempt that "failed" and burns a retry.
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IConsumeObserver.PreConsumeAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask PostConsumeCoreAsync(IConsumeObserver[] observers, ReceiveContext context, ConsumeOutcome outcome, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PostConsumeAsync(context, outcome, cancellationToken);
            }
            catch (Exception ex)
            {
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IConsumeObserver.PostConsumeAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask ConsumeFaultCoreAsync(IConsumeObserver[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.ConsumeFaultAsync(context, exception, cancellationToken);
            }
            catch (Exception ex)
            {
                // Isolation: the original fault is rethrown by the caller either way — an observer cannot swallow or replace it.
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IConsumeObserver.ConsumeFaultAsync), context.Envelope.MessageId);
            }
        }
    }

    [LoggerMessage(EventId = 6041, Level = LogLevel.Warning, Message = "Message observer {Observer} threw in {Hook} for message {MessageId}; ignored")]
    private static partial void LogObserverFailed(ILogger logger, Exception exception, string observer, string hook, string messageId);
}
