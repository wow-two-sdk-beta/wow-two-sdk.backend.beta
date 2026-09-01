using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Logging;

/// <summary>Provides conventional Serilog wiring behind the <c>ILogger&lt;T&gt;</c> seam.</summary>
public static class LoggingHostExtensions
{
    // {TraceId} renders Activity.Current's trace id, and renders empty when no activity is in scope.
    private const string ConsoleOutputTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}";

    private const string FileOutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{TraceId}] {Message:lj}{NewLine}{Exception}";

    /// <summary>Uses Serilog with defaults (console and rolling file in <c>logs/</c>, trace-tagged, enriched context); <c>Serilog:*</c> configuration overrides.</summary>
    /// <param name="host">The host builder to configure.</param>
    /// <remarks>
    ///   - both default sinks render the ambient trace id ahead of the message
    ///   - a sink added through <c>Serilog:WriteTo</c> carries its own template
    /// </remarks>
    public static IHostBuilder UseSerilogConventional(this IHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.UseSerilog((ctx, sp, lc) =>
        {
            lc.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(sp)
              .Enrich.FromLogContext()
              .Enrich.WithMachineName()
              .Enrich.WithProcessId()
              .Enrich.WithThreadId()
              .Enrich.WithEnvironmentName()
              .WriteTo.Async(a => a.Console(
                  outputTemplate: ConsoleOutputTemplate,
                  formatProvider: CultureInfo.InvariantCulture))
              .WriteTo.Async(a => a.File(
                  path: "logs/log-.txt",
                  outputTemplate: FileOutputTemplate,
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 7,
                  shared: true,
                  formatProvider: CultureInfo.InvariantCulture));
        });
    }
}
