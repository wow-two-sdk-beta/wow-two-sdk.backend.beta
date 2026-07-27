namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;

/// <summary>Defines the contract for rasterizing an SVG document to PNG bytes.</summary>
public interface ISvgRasterizer
{
    /// <summary>Renders <paramref name="svg"/> to PNG bytes, scaled so the output is at least <paramref name="minOutputPixels"/> on its longest side.</summary>
    /// <param name="svg">The SVG document to rasterize.</param>
    /// <param name="minOutputPixels">The minimum length, in pixels, of the output's longest side.</param>
    byte[] ToPng(string svg, int minOutputPixels = 1024);
}
