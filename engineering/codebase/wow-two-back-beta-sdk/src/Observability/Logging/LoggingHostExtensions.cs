using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Logging;

/// <summary>Provides conventional Serilog wiring behind the <c>ILogger&lt;T&gt;</c> seam.</summary>
public static class LoggingHostExtensions
{
    /// <summary>Uses Serilog with defaults (console and rolling file in <c>logs/</c>, enriched context); <c>Serilog:*</c> configuration overrides.</summary>
    /// <param name="host">The host builder to configure.</param>
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
              .WriteTo.Async(a => a.Console(formatProvider: CultureInfo.InvariantCulture))
              .WriteTo.Async(a => a.File(
                  path: "logs/log-.txt",
                  rollingInterval: RollingInterval.Day,
                  retainedFileCountLimit: 7,
                  shared: true,
                  formatProvider: CultureInfo.InvariantCulture));
        });
    }
}
