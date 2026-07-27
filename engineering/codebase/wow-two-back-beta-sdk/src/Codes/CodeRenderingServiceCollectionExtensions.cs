using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Svg;

namespace WoW.Two.Sdk.Backend.Beta.Codes;

/// <summary>DI registration for the code-rendering engine.</summary>
public static class CodeRenderingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the code-rendering engine — QR matrix generator, SVG renderer, SVG→raster rasterizer,
    /// QR + barcode renderers, and the unified <see cref="ICodeRenderer"/> facade — as singletons.
    /// </summary>
    /// <remarks>
    /// Rendering is stateless and thread-safe, so every component is a singleton. The product wires its own
    /// image/persistence services separately; this registers only the engine.
    /// </remarks>
    public static IServiceCollection AddCodeRendering(this IServiceCollection services)
    {
        services.AddSingleton<IQrMatrixGenerator, QrMatrixGenerator>();
        services.AddSingleton<SvgRenderer>();
        services.AddSingleton<ISvgRasterizer, SkiaSvgRasterizer>();
        services.AddSingleton<IQrCodeRenderer, QrCodeRenderer>();
        services.AddSingleton<IBarcodeRenderer, BarcodeRenderer>();
        services.AddSingleton<ICodeRenderer, CodeRenderer>();
        return services;
    }
}
