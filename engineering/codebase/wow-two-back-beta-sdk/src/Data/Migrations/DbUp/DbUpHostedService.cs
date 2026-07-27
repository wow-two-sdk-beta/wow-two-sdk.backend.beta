using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp;

/// <summary>Hosted service that runs DbUp at startup, applying any pending <c>.sql</c> scripts.</summary>
public sealed partial class DbUpHostedService : IHostedService
{
    private readonly DbUpOptions _options;
    private readonly ILogger<DbUpHostedService> _logger;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="options">The DbUp runner options.</param>
    /// <param name="logger">The logger for run progress and failures.</param>
    public DbUpHostedService(IOptions<DbUpOptions> options, ILogger<DbUpHostedService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            LogDisabled(_logger);
            return Task.CompletedTask;
        }

        if (_options.UpgradeEngineFactory is null)
            throw new InvalidOperationException(
                $"{nameof(DbUpOptions)}.{nameof(DbUpOptions.UpgradeEngineFactory)} must be set " +
                $"(use {nameof(DbUpProviderFactories)}.Postgres/SqlServer/MySql/Sqlite).");

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException($"{nameof(DbUpOptions)}.{nameof(DbUpOptions.ConnectionString)} is required.");

        var assembly = _options.ScriptsAssembly ?? Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("No scripts assembly resolved.");

        var builder = _options.UpgradeEngineFactory(_options.ConnectionString);

        builder = _options.ScriptsNamespacePrefix is { } prefix
            ? builder.WithScriptsEmbeddedInAssembly(assembly, name => name.StartsWith(prefix, StringComparison.Ordinal))
            : builder.WithScriptsEmbeddedInAssembly(assembly);

        var upgrader = builder
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            var scriptName = result.ErrorScript?.Name ?? "(unknown)";
            LogUpgradeFailed(_logger, scriptName, result.Error);
            throw new InvalidOperationException("DbUp upgrade failed.", result.Error);
        }

        var scriptCount = result.Scripts.Count();
        LogUpgradeApplied(_logger, scriptCount);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "DbUp runner is disabled — skipping.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Error, Message = "DbUp upgrade failed at script {Script}.")]
    private static partial void LogUpgradeFailed(ILogger logger, string script, Exception? exception);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Information, Message = "DbUp upgrade applied {Count} scripts.")]
    private static partial void LogUpgradeApplied(ILogger logger, int count);
}
