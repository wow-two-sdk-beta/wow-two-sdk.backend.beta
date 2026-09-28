using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Csv.Exporters;
using WoW.Two.Sdk.Backend.Beta.Media.Csv.Parsers;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Exporters;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Parsers;

namespace WoW.Two.Sdk.Backend.Beta.Media.Csv;

/// <summary>Registration helpers for CSV read/write.</summary>
public static class CsvServiceCollectionExtensions
{
    /// <summary>
    /// Registers CSV support: <see cref="ICsvParser"/> and <see cref="CsvTabularExporter"/>, each directly and as part
    /// of the <see cref="ITabularParser"/> / <see cref="ITabularExporter"/> sets. Singletons; idempotent.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCsvExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<CsvDocumentParser>();
        services.TryAddSingleton<ICsvParser>(provider => provider.GetRequiredService<CsvDocumentParser>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITabularParser, CsvDocumentParser>());
        services.TryAddSingleton<CsvTabularExporter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITabularExporter, CsvTabularExporter>());
        return services;
    }
}
