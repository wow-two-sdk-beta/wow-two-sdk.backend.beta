using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Otlp;

/// <summary>
/// OTLP exporter registration. Pair with <c>AddWowTwoTracing</c> and <c>AddWowTwoMetrics</c>.
/// </summary>
public static class OtlpServiceCollectionExtensions
{
    /// <summary>
    /// Register OTLP exporter on traces, metrics, and logs. Endpoint defaults to env <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> when not provided.
    /// </summary>
    public static IServiceCollection AddWowTwoOtlpExporter(this IServiceCollection services, Uri? endpoint = null, OtlpExportProtocol protocol = OtlpExportProtocol.Grpc)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureOpenTelemetryTracerProvider(b =>
            b.AddOtlpExporter(o =>
            {
                if (endpoint is not null) o.Endpoint = endpoint;
                o.Protocol = protocol;
            }));

        services.ConfigureOpenTelemetryMeterProvider(b =>
            b.AddOtlpExporter(o =>
            {
                if (endpoint is not null) o.Endpoint = endpoint;
                o.Protocol = protocol;
            }));

        return services;
    }
}
