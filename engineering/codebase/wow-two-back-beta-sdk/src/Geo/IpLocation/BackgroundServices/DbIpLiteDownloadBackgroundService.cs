using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.Services;

namespace WoW.Two.Sdk.Backend.Beta.Geo.IpLocation.BackgroundServices;

/// <summary>Runs the DB-IP Lite refresh at startup and then once per refresh interval.</summary>
/// <remarks>A failed refresh is logged and retried at the next interval; it never stops the host.</remarks>
public sealed partial class DbIpLiteDownloadBackgroundService : BackgroundService
{
    private readonly DbIpLiteDownloadService _downloads;
    private readonly DbIpLiteOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<DbIpLiteDownloadBackgroundService> _logger;

    /// <summary>Creates the refresh loop over the download service.</summary>
    /// <param name="downloads">The service that fetches and swaps releases.</param>
    /// <param name="options">The refresh interval.</param>
    /// <param name="clock">The clock that paces the interval.</param>
    /// <param name="logger">The logger for unexpected refresh faults.</param>
    public DbIpLiteDownloadBackgroundService(
        DbIpLiteDownloadService downloads,
        DbIpLiteOptions options,
        TimeProvider clock,
        ILogger<DbIpLiteDownloadBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(downloads);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _downloads = downloads;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.RefreshInterval, _clock);
        try
        {
            do
            {
                await RefreshOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown ends the loop; the last good database stays on disk.
        }
    }

    private async Task RefreshOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _downloads.RefreshAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRefreshFaulted(exception);
        }
    }

    [LoggerMessage(EventId = 7108, Level = LogLevel.Error, Message = "DB-IP Lite refresh faulted; retrying at the next interval")]
    private partial void LogRefreshFaulted(Exception exception);
}
