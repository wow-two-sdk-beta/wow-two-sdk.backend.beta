using System.Globalization;
using Serilog;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Logging.Services;

/// <summary>Provides an outer host boundary with a durable failure sink that exists before application DI and final logging.</summary>
public sealed class StartupFailureReportingService
{
    private const string DefaultPath = "logs/startup-failures.log";
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>Runs host creation, configuration and execution, records an escaping failure durably, flushes logging and rethrows the original exception.</summary>
    /// <param name="runHost">Creates, configures, builds and runs the host inside this outer boundary.</param>
    /// <param name="startupFailurePath">Independently readable startup-failure file. Default <c>logs/startup-failures.log</c>.</param>
    /// <param name="cancellationToken">Token passed to the host delegate.</param>
    public async Task RunAsync(
        Func<CancellationToken, Task> runHost,
        string startupFailurePath = DefaultPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runHost);
        ArgumentException.ThrowIfNullOrWhiteSpace(startupFailurePath);

        var previousLogger = Log.Logger;
        using var startupLogger = new LoggerConfiguration()
            .WriteTo.File(
                startupFailurePath,
                outputTemplate: OutputTemplate,
                shared: true,
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();
        Log.Logger = startupLogger;

        try
        {
            await runHost(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try
            {
                startupLogger.Fatal(exception, "Host terminated unexpectedly");
            }
            catch (Exception)
            {
                // Reporting cannot replace the original host failure.
            }

            throw;
        }
        finally
        {
            try
            {
                await Log.CloseAndFlushAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Flushing cannot replace the host result or its original failure.
            }
            finally
            {
                try
                {
                    startupLogger.Dispose();
                }
                catch (Exception)
                {
                    // Disposal cannot replace the host result or its original failure.
                }

                Log.Logger = previousLogger;
            }
        }
    }
}
