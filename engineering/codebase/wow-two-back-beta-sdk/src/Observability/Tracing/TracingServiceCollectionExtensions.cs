using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Tracing;

/// <summary>Provides OpenTelemetry tracer registration with conventional auto-instrumentation.</summary>
public static class TracingServiceCollectionExtensions
{
    /// <summary>Adds OpenTelemetry tracing auto-instrumented for ASP.NET Core, HttpClient, gRPC, SqlClient, EF Core, and Redis; no exporters (pair with <c>Observability.Otlp</c>, <c>.Prometheus</c>, etc.).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="serviceName">Logical service name for the resource.</param>
    public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                // Collect every brand-prefixed ActivitySource (WoW.Two.Messaging, …) — wildcard match.
                tracing.AddSource("WoW.Two.*");
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation();
                tracing.AddGrpcClientInstrumentation();
                tracing.AddSqlClientInstrumentation();
                tracing.AddEntityFrameworkCoreInstrumentation();
                tracing.AddRedisInstrumentation();
            });

        return services;
    }
}
