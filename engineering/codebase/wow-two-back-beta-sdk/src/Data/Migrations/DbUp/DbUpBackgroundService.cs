using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Data.Migrations.DbUp;

/// <summary>Runs pending DbUp scripts during host startup.</summary>
/// <param name="options">The DbUp runner options.</param>
/// <param name="logger">The logger for run progress and failures.</param>
public sealed partial class DbUpBackgroundService(DbUpOptions options, ILogger<DbUpBackgroundService> logger) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            LogDisabled(logger);
            return Task.CompletedTask;
        }

        if (options.UpgradeEngineFactory is null)
            throw new InvalidOperationException(
                $"{nameof(DbUpOptions)}.{nameof(DbUpOptions.UpgradeEngineFactory)} must be set " +
                "(call UsePostgres(), UseSqlServer() or UseMySql() on the options, or assign UpgradeEngineFactory).");

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new InvalidOperationException($"{nameof(DbUpOptions)}.{nameof(DbUpOptions.ConnectionString)} is required.");

        var assembly = options.ScriptsAssembly ?? Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("No scripts assembly resolved.");

        var builder = options.UpgradeEngineFactory(options.ConnectionString);

        builder = options.ScriptsNamespacePrefix is { } prefix
            ? builder.WithScriptsEmbeddedInAssembly(assembly, name => name.StartsWith(prefix, StringComparison.Ordinal))
            : builder.WithScriptsEmbeddedInAssembly(assembly);

        var upgrader = builder
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            var scriptName = result.ErrorScript?.Name ?? "(unknown)";
            LogUpgradeFailed(logger, scriptName, result.Error);
            throw new InvalidOperationException("DbUp upgrade failed.", result.Error);
        }

        var scriptCount = result.Scripts.Count();
        LogUpgradeApplied(logger, scriptCount);
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
