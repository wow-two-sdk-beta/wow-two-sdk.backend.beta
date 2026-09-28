using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Excel.Exporters;
using WoW.Two.Sdk.Backend.Beta.Media.Excel.Parsers;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Exporters;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Parsers;

namespace WoW.Two.Sdk.Backend.Beta.Media.Excel;

/// <summary>Registration helpers for Excel export.</summary>
public static class ExcelServiceCollectionExtensions
{
    /// <summary>
    /// Registers XLSX support: <see cref="ExcelTabularExporter"/> and <see cref="ExcelDocumentParser"/>, each directly and
    /// as part of the <see cref="ITabularExporter"/> / <see cref="ITabularParser"/> sets, plus <see cref="IExcelParser"/>.
    /// A cell that does not convert maps to a 400 through <see cref="MediaExceptionMappingRule"/>. Singletons; idempotent.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddExcelExport(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ExcelTabularExporter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITabularExporter, ExcelTabularExporter>());
        services.TryAddSingleton<ExcelDocumentParser>();
        services.TryAddSingleton<IExcelParser>(provider => provider.GetRequiredService<ExcelDocumentParser>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITabularParser, ExcelDocumentParser>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionMappingRule, MediaExceptionMappingRule>());
        return services;
    }
}
