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

namespace WoW.Two.Sdk.Backend.Beta.Messaging.Transport;

/// <summary>
/// Deletes claim-checked blobs once they age past <see cref="ClaimCheckOptions.Retention"/>. The only thing that ever
/// deletes one — consuming a message does not, because the same blob still has to serve the other subscribers of a
/// fan-out, the next retry, and any later redrive out of the dead-letter store.
/// </summary>
internal sealed partial class ClaimCheckRetentionSweeper(
    ClaimCheckPayloadRepository store,
    ClaimCheckOptions options,
    TimeProvider timeProvider,
    ILogger<ClaimCheckRetentionSweeper> logger) : BackgroundService
{
    // Bounds the first pass over a store that has been accumulating without a sweep; the remainder goes next pass.
    private const int MaxDeletesPerSweep = 1_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = options;
        if (!config.Enabled || !config.SweepEnabled)
        {
            LogSweepDisabled();
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await store.SweepAsync(timeProvider.GetUtcNow() - config.Retention, MaxDeletesPerSweep, stoppingToken);
                if (deleted > 0)
                    LogSwept(deleted, config.Retention);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // A failed sweep must not take the host down — the next pass tries again.
                LogSweepFailed(exception);
            }

            try
            {
                await Task.Delay(config.SweepInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    [LoggerMessage(EventId = 6083, Level = LogLevel.Debug, Message = "Claim-check retention sweep is disabled; offloaded bodies are expected to expire under the blob store's own lifecycle rules")]
    private partial void LogSweepDisabled();

    [LoggerMessage(EventId = 6084, Level = LogLevel.Information, Message = "Swept {DeletedCount} claim-check bodies older than {Retention}")]
    private partial void LogSwept(int deletedCount, TimeSpan retention);

    [LoggerMessage(EventId = 6085, Level = LogLevel.Warning, Message = "Claim-check retention sweep failed; retrying at the next interval")]
    private partial void LogSweepFailed(Exception exception);
}
