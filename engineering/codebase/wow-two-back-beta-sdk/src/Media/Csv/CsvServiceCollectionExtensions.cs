using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;

namespace WoW.Two.Sdk.Backend.Beta.Media.Csv;

/// <summary>Registration helpers for CSV read/write.</summary>
public static class CsvServiceCollectionExtensions
{
    /// <summary>
    /// Registers CSV support: <see cref="ICsvReader"/> and <see cref="CsvTabularExporter"/> (both directly and
    /// as part of the <see cref="ITabularExporter"/> set). Singletons; idempotent.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCsvExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ICsvReader, CsvDocumentReader>();
        services.TryAddSingleton<CsvTabularExporter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITabularExporter, CsvTabularExporter>());
        return services;
    }
}
