using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Tracing;

/// <summary>
/// OpenTelemetry tracer registration with conventional auto-instrumentation.
/// </summary>
public static class TracingServiceCollectionExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing with auto-instrumentation for ASP.NET Core, HttpClient, gRPC, SqlClient, EF Core, and StackExchange.Redis.
    /// Exporters are NOT registered here — pair with `Observability.Otlp`, `.Prometheus`, etc.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="serviceName">Logical service name (used for the OTel resource).</param>
    public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
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
