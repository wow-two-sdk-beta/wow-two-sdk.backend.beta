using Microsoft.Extensions.Logging;

namespace WoW.Two.Sdk.Backend.Beta.Jobs.Batches;

/// <summary>Extends the batch pipeline domain with its log messages — static because the pipeline is generic and the logging source generator emits into the declaring type.</summary>
internal static partial class BatchPipelineLogExtensions
{
    [LoggerMessage(EventId = 8001, Level = LogLevel.Warning, Message = "Batch pipeline {Pipeline} rejected items; total rejected {Count}")]
    public static partial void BatchItemsDropped(this ILogger logger, string pipeline, long count);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Error, Message = "Batch pipeline {Pipeline} failed a batch of {Count} items")]
    public static partial void BatchFailed(this ILogger logger, Exception exception, string pipeline, int count);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Warning, Message = "Batch pipeline {Pipeline} stopped with {Count} accepted items unhandled")]
    public static partial void BatchItemsAbandoned(this ILogger logger, string pipeline, long count);
}
