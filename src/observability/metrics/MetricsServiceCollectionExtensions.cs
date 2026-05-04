using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Metrics;

/// <summary>
/// OpenTelemetry meter registration with conventional auto-instrumentation.
/// </summary>
public static class MetricsServiceCollectionExtensions
{
    /// <summary>
    /// Registers OpenTelemetry metrics with auto-instrumentation for ASP.NET Core, HttpClient, runtime, and process.
    /// Exporters are NOT registered here — pair with `Observability.Otlp`, `.Prometheus`, etc.
    /// </summary>
    public static IServiceCollection AddWowTwoMetrics(this IServiceCollection services, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();
                metrics.AddProcessInstrumentation();
            });

        return services;
    }
}
