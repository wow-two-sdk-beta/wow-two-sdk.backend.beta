using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Media.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Images;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>PDF processing registration.</summary>
public static class PdfServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPdfService"/> over PDFsharp and PdfPig, with image processing for image pages, and maps
    /// <see cref="PdfRejectedException"/> to a 400. Options come from <paramref name="configure"/>, then the host section
    /// <c>Media:Pdf</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Limits and fonts.</param>
    public static IServiceCollection AddPdfProcessing(this IServiceCollection services, Action<PdfOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            PdfOptions.SectionName,
            configure,
            builder => builder.Validate(o => o.MaxInputBytes > 0 && o.MaxPages > 0, "PdfOptions limits must be positive."));
        services.AddImageProcessing();
        services.TryAddSingleton<IPdfService, PdfSharpPdfService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionMappingRule, MediaExceptionMappingRule>());
        return services;
    }
}
