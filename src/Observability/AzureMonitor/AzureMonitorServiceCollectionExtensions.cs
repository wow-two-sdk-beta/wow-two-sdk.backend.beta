using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace WoW.Two.Sdk.Backend.Beta.Observability.AzureMonitor;

/// <summary>
/// Azure Monitor OpenTelemetry distro registration.
/// </summary>
public static class AzureMonitorServiceCollectionExtensions
{
    /// <summary>
    /// Wire the Azure Monitor distro. Reads `APPLICATIONINSIGHTS_CONNECTION_STRING` env var if no explicit connection string is provided.
    /// </summary>
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
