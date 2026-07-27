using SkiaSharp;
using Svg.Skia;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;

/// <summary>Provides a Svg.Skia (SkiaSharp) <see cref="ISvgRasterizer"/> that renders the emitter's SVG to PNG with no <c>System.Drawing</c> dependency.</summary>
public sealed class SkiaSvgRasterizer : ISvgRasterizer
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Svg.Skia could not parse the supplied SVG.</exception>
    public byte[] ToPng(string svg, int minOutputPixels = 1024)
    {
        using var skSvg = new SKSvg();
        using var picture = skSvg.FromSvg(svg)
            ?? throw new InvalidOperationException("Svg.Skia failed to parse the emitted SVG.");

        var bounds = picture.CullRect;
        var srcWidth = bounds.Width > 0 ? bounds.Width : 1f;
        var srcHeight = bounds.Height > 0 ? bounds.Height : 1f;

        // Scale up so the longest side reaches the print floor (≈300+ DPI), but never downscale the vector source.
        var longest = Math.Max(srcWidth, srcHeight);
        var scale = Math.Max(1f, minOutputPixels / longest);

        var width = (int)Math.Ceiling(srcWidth * scale);
        var height = (int)Math.Ceiling(srcHeight * scale);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;

        // Transparent canvas: a transparent-background SVG (no <rect>) rasterizes with a clear backdrop;
        // a solid-background SVG paints its own rect over this.
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }
}
