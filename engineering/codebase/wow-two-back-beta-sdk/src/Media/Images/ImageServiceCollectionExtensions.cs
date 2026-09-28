using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;
using WoW.Two.Sdk.Backend.Beta.Media.Errors;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Image processing registration.</summary>
public static class ImageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IImageService"/> over SkiaSharp and maps <see cref="ImageRejectedException"/> to a 400.
    /// Options come from <paramref name="configure"/>, then the host section <c>Media:Images</c>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Limits, encoder defaults and fonts.</param>
    public static IServiceCollection AddImageProcessing(this IServiceCollection services, Action<ImageOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddModuleOptions(
            ImageOptions.SectionName,
            configure,
            builder => builder
                .Validate(o => o.MaxInputBytes > 0 && o.MaxPixels > 0 && o.MaxOutputDimension > 0, "ImageOptions limits must be positive.")
                .Validate(o => o.JpegQuality is >= 1 and <= 100 && o.WebpQuality is >= 1 and <= 100 && o.MinQuality is >= 1 and <= 100, "ImageOptions qualities must lie between 1 and 100."));
        services.TryAddSingleton<IImageService, SkiaImageService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionMappingRule, MediaExceptionMappingRule>());
        return services;
    }
}
