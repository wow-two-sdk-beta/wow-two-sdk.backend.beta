using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace WoW.Two.Sdk.Backend.Beta.Caching.Invalidation.BackgroundServices;

/// <summary>Runs a PostgreSQL LISTEN loop and evicts the local entries each committed notification names.</summary>
/// <remarks>
///   - every successful subscription first evicts everything, since notifications sent while disconnected are lost
///   - a lost connection is logged and retried after the reconnect delay; it never stops the host
///   - an unknown payload is ignored and logged
/// </remarks>
public sealed partial class PostgresCacheInvalidationBackgroundService : BackgroundService
{
    private readonly PostgresCacheInvalidationOptions _options;
    private readonly ICacheInvalidationHandler _handler;
    private readonly TimeProvider _clock;
    private readonly ILogger<PostgresCacheInvalidationBackgroundService> _logger;

    /// <summary>Creates the loop over the configured database and channel.</summary>
    /// <param name="options">The connection, channel and reconnect delay.</param>
    /// <param name="handler">The local eviction handler.</param>
    /// <param name="clock">The clock that paces reconnects.</param>
    /// <param name="logger">The logger for connection and payload faults.</param>
    public PostgresCacheInvalidationBackgroundService(
        PostgresCacheInvalidationOptions options,
        ICacheInvalidationHandler handler,
        TimeProvider clock,
        ILogger<PostgresCacheInvalidationBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _handler = handler;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or IOException)
            {
                LogListenerLost(exception, _options.Channel);
            }

            try
            {
                await Task.Delay(_options.ReconnectDelay, _clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task ListenAsync(CancellationToken stoppingToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(_options.ConnectionString) { KeepAlive = 30 };
        if (string.IsNullOrWhiteSpace(builder.ApplicationName))
        {
            builder.ApplicationName = CacheInvalidationConstants.ListenerApplicationName;
        }

        var pending = new Queue<string>();
        var connection = new NpgsqlConnection(builder.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            connection.Notification += (_, notification) => pending.Enqueue(notification.Payload);
            await connection.OpenAsync(stoppingToken).ConfigureAwait(false);
            var listen = new NpgsqlCommand($"LISTEN \"{_options.Channel.Replace("\"", "\"\"", StringComparison.Ordinal)}\"", connection);
            await using (listen.ConfigureAwait(false))
            {
                await listen.ExecuteNonQueryAsync(stoppingToken).ConfigureAwait(false);
            }

            await _handler.InvalidateAllAsync(stoppingToken).ConfigureAwait(false);
            LogListening(_options.Channel);
            while (true)
            {
                await connection.WaitAsync(stoppingToken).ConfigureAwait(false);
                while (pending.TryDequeue(out string? payload))
                {
                    await DispatchAsync(payload, stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task DispatchAsync(string payload, CancellationToken stoppingToken)
    {
        try
        {
            if (payload.StartsWith(CacheInvalidationConstants.KeyPrefix, StringComparison.Ordinal))
            {
                await _handler.InvalidateKeyAsync(payload[CacheInvalidationConstants.KeyPrefix.Length..], stoppingToken).ConfigureAwait(false);
            }
            else if (payload.StartsWith(CacheInvalidationConstants.TagPrefix, StringComparison.Ordinal))
            {
                await _handler.InvalidateTagAsync(payload[CacheInvalidationConstants.TagPrefix.Length..], stoppingToken).ConfigureAwait(false);
            }
            else
            {
                LogUnknownPayload(_options.Channel);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogEvictionFailed(exception, _options.Channel);
        }
    }

    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "Listening for cache invalidations on {Channel}; local entries were evicted")]
    private partial void LogListening(string channel);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Warning, Message = "Cache invalidation listener on {Channel} lost its connection; reconnecting")]
    private partial void LogListenerLost(Exception exception, string channel);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Warning, Message = "Ignored a cache invalidation on {Channel} with an unknown payload")]
    private partial void LogUnknownPayload(string channel);

    [LoggerMessage(EventId = 9004, Level = LogLevel.Error, Message = "A cache invalidation on {Channel} failed to evict; entries expire by their lifetime")]
    private partial void LogEvictionFailed(Exception exception, string channel);
}
