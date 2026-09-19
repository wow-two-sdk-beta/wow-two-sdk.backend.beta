using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors.Mappers;

namespace WoW.Two.Sdk.Backend.Beta.Observability.Errors;

/// <summary>Provides registration for the error-observability seam.</summary>
public static class AppErrorObserverServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ErrorRecordingService"/> and the default <see cref="IErrorNatureMapper"/> it depends on.</summary>
    /// <param name="services">The service collection to configure.</param>
    public static IServiceCollection AddErrorRecordingService(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IErrorNatureMapper, ErrorNatureMapper>();
        services.TryAddSingleton<ErrorRecordingService>();

        return services;
    }
}
