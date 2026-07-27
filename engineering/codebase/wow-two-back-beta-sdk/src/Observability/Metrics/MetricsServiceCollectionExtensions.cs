using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Metrics;

/// <summary>Provides OpenTelemetry meter registration with conventional auto-instrumentation.</summary>
public static class MetricsServiceCollectionExtensions
{
    /// <summary>Adds OpenTelemetry metrics auto-instrumented for ASP.NET Core, HttpClient, runtime, and process; no exporters (pair with <c>Observability.Otlp</c>, <c>.Prometheus</c>, etc.).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceName">Logical service name for the resource.</param>
    public static IServiceCollection AddOpenTelemetryMetrics(this IServiceCollection services, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter("WoW.Two.*"); // collect every SDK-owned meter (errors, messaging, …); mirrors tracing's AddSource("WoW.Two.*")
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddRuntimeInstrumentation();
                metrics.AddProcessInstrumentation();
            });

        return services;
    }
}
