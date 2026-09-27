using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Data.Sessions;

/// <summary>Extends the data-session domain with its log messages — static because the session is generic and the logging source generator emits into the declaring type.</summary>
internal static partial class DataSessionLogExtensions
{
    [LoggerMessage(EventId = 3301, Level = LogLevel.Error, Message = "Data-session transaction cleanup failed in state {State}")]
    public static partial void TransactionCleanupFailed(this ILogger logger, Exception exception, DataSessionState state);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Error, Message = "Cleanup could not confirm rollback after an uncertain commit; reconcile before retrying")]
    public static partial void UncertainCommitCleanupFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3303, Level = LogLevel.Error, Message = "Data-session callback failed; completed {Completed} of {Total}, state {State}")]
    public static partial void CallbackFailed(this ILogger logger, Exception exception, int completed, int total, DataSessionState state);
}
