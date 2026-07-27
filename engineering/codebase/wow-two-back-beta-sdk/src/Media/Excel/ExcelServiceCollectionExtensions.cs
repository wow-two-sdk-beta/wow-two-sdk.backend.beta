using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;

namespace WoW.Two.Sdk.Backend.Beta.Media.Excel;

/// <summary>Registration helpers for Excel export.</summary>
public static class ExcelServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ExcelTabularExporter"/> (directly and as part of the <see cref="ITabularExporter"/>
    /// set) as a singleton. Idempotent.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddExcelExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ExcelTabularExporter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITabularExporter, ExcelTabularExporter>());
        return services;
    }
}
