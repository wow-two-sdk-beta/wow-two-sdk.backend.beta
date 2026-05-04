using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Logging;

/// <summary>
/// Conventional Serilog → ILogger wiring. Public seam stays `ILogger&lt;T&gt;`; Serilog is the under-the-hood provider.
/// </summary>
public static class LoggingHostExtensions
{
    /// <summary>
    /// Wire Serilog with sane defaults: console + rolling file in <c>logs/</c>, enriched with machine/process/thread context.
    /// Honors `Serilog:*` configuration if present (overrides programmatic defaults).
    /// </summary>
    public static IHostBuilder UseWowTwoLogging(this IHostBuilder host)
    {
        ArgumentNullException.ThrowIfNull(host);

        return host.UseSerilog((ctx, sp, lc) =>
        {
            // Programmatic defaults
            lc.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(sp)
              .Enrich.FromLogContext()
              .Enrich.WithMachineName()
              .Enrich.WithProcessId()
              .Enrich.WithThreadId()
              .Enrich.WithEnvironmentName()
              .WriteTo.Async(a => a.Console())
              .WriteTo.Async(a => a.File(
                  path: "logs/log-.txt",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 7,
                  shared: true));
        });
    }
}
