using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Saga;

/// <summary>Saga log messages. Non-generic on purpose — the logging source generator emits into the declaring type, and the coordinator is generic.</summary>
internal static partial class SagaLog
{
    [LoggerMessage(EventId = 6111, Level = LogLevel.Debug, Message = "Saga {Saga}: {Event} {MessageId} carries no correlation key; ignored")]
    public static partial void Uncorrelated(ILogger logger, string saga, string @event, string messageId);

    [LoggerMessage(EventId = 6112, Level = LogLevel.Debug, Message = "Saga {Saga}: no instance {CorrelationId} for {Event}, and no clause initiates one; ignored")]
    public static partial void InstanceNotFound(ILogger logger, string saga, string @event, string correlationId);

    [LoggerMessage(EventId = 6113, Level = LogLevel.Debug, Message = "Saga {Saga}: {Event} has no clause in state {State} (instance {CorrelationId}); ignored")]
    public static partial void NoTransition(ILogger logger, string saga, string @event, string state, string correlationId);

    [LoggerMessage(EventId = 6114, Level = LogLevel.Debug, Message = "Saga {Saga}: timeout {Timeout} for instance {CorrelationId} was cancelled or superseded; dropped")]
    public static partial void StaleTimeout(ILogger logger, string saga, string timeout, string correlationId);

    [LoggerMessage(EventId = 6115, Level = LogLevel.Debug, Message = "Saga {Saga}: instance {CorrelationId} changed concurrently on attempt {Attempt}; reloading and replaying the transition")]
    public static partial void ConcurrencyConflict(ILogger logger, string saga, string correlationId, int attempt);

    [LoggerMessage(EventId = 6116, Level = LogLevel.Error, Message = "Saga {Saga}: instance {CorrelationId} still contended after {Attempts} attempts; the message goes to retry / dead-letter")]
    public static partial void ConcurrencyExhausted(ILogger logger, Exception exception, string saga, string correlationId, int attempts);

    [LoggerMessage(EventId = 6117, Level = LogLevel.Debug, Message = "Saga {Saga} instance {CorrelationId}: {Event} moved {From} -> {To}")]
    public static partial void Transitioned(ILogger logger, string saga, string correlationId, string @event, string from, string to);
}
