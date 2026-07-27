using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Prometheus;

/// <summary>Provides Prometheus scrape-exporter registration. Pair with <c>AddOpenTelemetryMetrics</c>.</summary>
public static class PrometheusServiceCollectionExtensions
{
    /// <summary>Adds the Prometheus scrape-endpoint exporter. After build, call <c>app.MapPrometheusScrapingEndpoint()</c>.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddPrometheusMetricsExporter(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureOpenTelemetryMeterProvider(b => b.AddPrometheusExporter());

        return services;
    }
}
