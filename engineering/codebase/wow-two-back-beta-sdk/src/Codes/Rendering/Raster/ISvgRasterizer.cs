namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;

/// <summary>Defines rasterizing the code engine's bounded SVG subset to PNG bytes.</summary>
public interface ISvgRasterizer
{
    /// <summary>Renders <paramref name="svg"/> to PNG bytes, scaled so the output is at least <paramref name="minOutputPixels"/> on its longest side.</summary>
    /// <param name="svg">The code-engine SVG to rasterize; active/external SVG content is rejected.</param>
    /// <param name="minOutputPixels">The minimum length, in pixels, of the output's longest side.</param>
    byte[] ToPng(string svg, int minOutputPixels = 1024);
}
