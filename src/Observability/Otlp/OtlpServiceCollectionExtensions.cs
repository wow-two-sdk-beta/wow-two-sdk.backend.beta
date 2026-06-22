using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Otlp;

/// <summary>Provides OTLP exporter registration. Pair with <c>AddOpenTelemetryTracing</c> and <c>AddOpenTelemetryMetrics</c>.</summary>
public static class OtlpServiceCollectionExtensions
{
    /// <summary>Adds the OTLP exporter on traces, metrics, and logs (endpoint falls back to <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="endpoint">OTLP collector endpoint; when omitted, the environment variable is used.</param>
    /// <param name="protocol">OTLP wire protocol for the exporter.</param>
    public static IServiceCollection AddOtlpExporters(this IServiceCollection services, Uri? endpoint = null, OtlpExportProtocol protocol = OtlpExportProtocol.Grpc)
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
