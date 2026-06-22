using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Observability.AzureMonitor;

/// <summary>Provides Azure Monitor OpenTelemetry distro registration.</summary>
public static class AzureMonitorServiceCollectionExtensions
{
    /// <summary>Adds the Azure Monitor distro (falls back to the <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> env var).</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="connectionString">Azure Monitor connection string; when omitted, the environment variable is used.</param>
    public static IServiceCollection AddAzureMonitorExporter(this IServiceCollection services, string? connectionString = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOpenTelemetry().UseAzureMonitor(o =>
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
                o.ConnectionString = connectionString;
        });

        return services;
    }
}
