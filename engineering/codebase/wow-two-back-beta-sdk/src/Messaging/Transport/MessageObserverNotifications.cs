using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Fan-out helpers that notify the registered observers from the bus and the processing pipeline.
/// </summary>
/// <remarks>
///   - each hook runs in its own try/catch per observer — a throwing observer is logged and skipped
///   - no observer can break message processing or change settlement
/// </remarks>
internal static partial class MessageObserverNotifications
{
    /// <summary>True when the exception is co-operative cancellation of <paramref name="cancellationToken"/> rather than a real fault — shutdown is not something to report as a fault.</summary>
    /// <param name="exception">The exception under inspection.</param>
    /// <param name="cancellationToken">The token driving the operation.</param>
    public static bool IsCancellation(Exception exception, CancellationToken cancellationToken)
        => exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    /// <summary>Notify <see cref="IPublishObservingInterceptor.PrePublishAsync"/>.</summary>
    /// <param name="observers">The registered publish observers.</param>
    /// <param name="envelope">The envelope about to be sent.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPrePublishAsync(this IPublishObservingInterceptor[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PrePublishCoreAsync(observers, envelope, logger, cancellationToken);

    /// <summary>Notify <see cref="IPublishObservingInterceptor.PostPublishAsync"/>.</summary>
    /// <param name="observers">The registered publish observers.</param>
    /// <param name="envelope">The envelope that was sent.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPostPublishAsync(this IPublishObservingInterceptor[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PostPublishCoreAsync(observers, envelope, logger, cancellationToken);

    /// <summary>Notify <see cref="IPublishObservingInterceptor.PublishFaultAsync"/>.</summary>
    /// <param name="observers">The registered publish observers.</param>
    /// <param name="envelope">The envelope that failed to send.</param>
    /// <param name="exception">The fault thrown by the transport.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPublishFaultAsync(this IPublishObservingInterceptor[] observers, EventEnvelope envelope, Exception exception, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PublishFaultCoreAsync(observers, envelope, exception, logger, cancellationToken);

    /// <summary>Notify <see cref="IReceiveObservingInterceptor.PreReceiveAsync"/>.</summary>
    /// <param name="observers">The registered receive observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPreReceiveAsync(this IReceiveObservingInterceptor[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PreReceiveCoreAsync(observers, context, logger, cancellationToken);

    /// <summary>Notify <see cref="IReceiveObservingInterceptor.PostReceiveAsync"/>.</summary>
    /// <param name="observers">The registered receive observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPostReceiveAsync(this IReceiveObservingInterceptor[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PostReceiveCoreAsync(observers, context, logger, cancellationToken);

    /// <summary>Notify <see cref="IReceiveObservingInterceptor.ReceiveFaultAsync"/>.</summary>
    /// <param name="observers">The registered receive observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The terminal fault.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyReceiveFaultAsync(this IReceiveObservingInterceptor[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : ReceiveFaultCoreAsync(observers, context, exception, logger, cancellationToken);

    /// <summary>Notify <see cref="IConsumeObservingInterceptor.PreConsumeAsync"/>.</summary>
    /// <param name="observers">The registered consume observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPreConsumeAsync(this IConsumeObservingInterceptor[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PreConsumeCoreAsync(observers, context, logger, cancellationToken);

    /// <summary>Notify <see cref="IConsumeObservingInterceptor.PostConsumeAsync"/>.</summary>
    /// <param name="observers">The registered consume observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="outcome">The outcome recorded for this attempt.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyPostConsumeAsync(this IConsumeObservingInterceptor[] observers, ReceiveContext context, ConsumeOutcome outcome, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : PostConsumeCoreAsync(observers, context, outcome, logger, cancellationToken);

    /// <summary>Notify <see cref="IConsumeObservingInterceptor.ConsumeFaultAsync"/>.</summary>
    /// <param name="observers">The registered consume observers.</param>
    /// <param name="context">The receive context.</param>
    /// <param name="exception">The fault thrown by the attempt.</param>
    /// <param name="logger">Logger for observer faults.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static ValueTask NotifyConsumeFaultAsync(this IConsumeObservingInterceptor[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
        => observers.Length == 0 ? ValueTask.CompletedTask : ConsumeFaultCoreAsync(observers, context, exception, logger, cancellationToken);

    private static async ValueTask PrePublishCoreAsync(IPublishObservingInterceptor[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
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
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IPublishObservingInterceptor.PrePublishAsync), envelope.MessageId);
            }
        }
    }

    private static async ValueTask PostPublishCoreAsync(IPublishObservingInterceptor[] observers, EventEnvelope envelope, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PostPublishAsync(envelope, cancellationToken);
            }
            catch (Exception ex)
            {
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IPublishObservingInterceptor.PostPublishAsync), envelope.MessageId);
            }
        }
    }

    private static async ValueTask PublishFaultCoreAsync(IPublishObservingInterceptor[] observers, EventEnvelope envelope, Exception exception, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PublishFaultAsync(envelope, exception, cancellationToken);
            }
            catch (Exception ex)
            {
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IPublishObservingInterceptor.PublishFaultAsync), envelope.MessageId);
            }
        }
    }

    private static async ValueTask PreReceiveCoreAsync(IReceiveObservingInterceptor[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
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
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IReceiveObservingInterceptor.PreReceiveAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask PostReceiveCoreAsync(IReceiveObservingInterceptor[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
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
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IReceiveObservingInterceptor.PostReceiveAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask ReceiveFaultCoreAsync(IReceiveObservingInterceptor[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
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
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IReceiveObservingInterceptor.ReceiveFaultAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask PreConsumeCoreAsync(IConsumeObservingInterceptor[] observers, ReceiveContext context, ILogger logger, CancellationToken cancellationToken)
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
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IConsumeObservingInterceptor.PreConsumeAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask PostConsumeCoreAsync(IConsumeObservingInterceptor[] observers, ReceiveContext context, ConsumeOutcome outcome, ILogger logger, CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.PostConsumeAsync(context, outcome, cancellationToken);
            }
            catch (Exception ex)
            {
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IConsumeObservingInterceptor.PostConsumeAsync), context.Envelope.MessageId);
            }
        }
    }

    private static async ValueTask ConsumeFaultCoreAsync(IConsumeObservingInterceptor[] observers, ReceiveContext context, Exception exception, ILogger logger, CancellationToken cancellationToken)
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
                LogObserverFailed(logger, ex, observer.GetType().Name, nameof(IConsumeObservingInterceptor.ConsumeFaultAsync), context.Envelope.MessageId);
            }
        }
    }

    [LoggerMessage(EventId = 6041, Level = LogLevel.Warning, Message = "Message observer {Observer} threw in {Hook} for message {MessageId}; ignored")]
    private static partial void LogObserverFailed(ILogger logger, Exception exception, string observer, string hook, string messageId);
}
