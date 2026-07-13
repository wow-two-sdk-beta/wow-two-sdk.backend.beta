using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Registration helpers for the Media.Captions package.</summary>
public static class CaptionParsingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IVttCaptionParser"/> (implemented by <see cref="VttCaptionParser"/>) as a singleton.
    /// The parser is stateless and thread-safe; the call is idempotent.
    /// </summary>
    /// <param name="services">The service collection to add the parser to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddVttCaptionParser(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IVttCaptionParser, VttCaptionParser>();
        return services;
    }

    /// <summary>
    /// Registers the full caption surface as singletons: every per-format parser (WebVTT, SubRip, TTML,
    /// YouTube json3), the format-detecting <see cref="ICaptionParser"/> facade, and the VTT + SRT writers
    /// (exposed both individually and via the <see cref="ICaptionWriter"/> set for conversion). All
    /// implementations are stateless and thread-safe; the call is idempotent.
    /// </summary>
    /// <param name="services">The service collection to add the caption services to.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddCaptionParsing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IVttCaptionParser, VttCaptionParser>();
        services.TryAddSingleton<ISrtCaptionParser, SrtCaptionParser>();
        services.TryAddSingleton<ITtmlCaptionParser, TtmlCaptionParser>();
        services.TryAddSingleton<IJson3CaptionParser, Json3CaptionParser>();
        services.TryAddSingleton<ICaptionParser, CompositeCaptionParser>();

        services.TryAddSingleton<VttCaptionWriter>();
        services.TryAddSingleton<SrtCaptionWriter>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICaptionWriter, VttCaptionWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ICaptionWriter, SrtCaptionWriter>());

        return services;
    }
}
