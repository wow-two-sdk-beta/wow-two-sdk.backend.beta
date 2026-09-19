using SkiaSharp;
using Svg.Skia;
using WoW.Two.Sdk.Backend.Beta.Codes.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;

/// <summary>Provides a Svg.Skia (SkiaSharp) <see cref="ISvgRasterizer"/> that renders the emitter's SVG to PNG with no <c>System.Drawing</c> dependency.</summary>
public sealed class SkiaSvgRasterizer : ISvgRasterizer
{
    private readonly CodeSvgValidator _validator = new();
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Svg.Skia could not parse the supplied SVG.</exception>
    public byte[] ToPng(string svg, int minOutputPixels = 1024)
    {
        if (minOutputPixels is < 1 or > 4096)
            throw CodeRenderValidationExtensions.Invalid("MinOutputPixels", "Use an output size from 1 to 4096 pixels.");
        _validator.Validate(svg);
        using var skSvg = new SKSvg();
        SKPicture picture;
        try
        {
            picture = skSvg.FromSvg(svg)
                ?? throw CodeRenderValidationExtensions.Invalid("Svg", "SVG could not be rendered.");
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or System.Xml.XmlException or OverflowException)
        {
            throw CodeRenderValidationExtensions.Invalid("Svg", "SVG could not be rendered.");
        }

        var bounds = picture.CullRect;
        var srcWidth = bounds.Width;
        var srcHeight = bounds.Height;
        if (!float.IsFinite(srcWidth) || !float.IsFinite(srcHeight) || srcWidth <= 0 || srcHeight <= 0)
            throw CodeRenderValidationExtensions.Invalid("Svg", "SVG must have finite positive dimensions.");

        // Scale up so the longest side reaches the print floor (≈300+ DPI), but never downscale the vector source.
        var longest = Math.Max(srcWidth, srcHeight);
        var scale = Math.Max(1f, minOutputPixels / longest);

        if (srcWidth * scale > 4096 || srcHeight * scale > 4096
            || Math.Ceiling(srcWidth * scale) * Math.Ceiling(srcHeight * scale) > 16_000_000)
            throw CodeRenderValidationExtensions.Invalid("Svg", "Raster dimensions exceed the rendering budget.");
        var width = (int)Math.Ceiling(srcWidth * scale);
        var height = (int)Math.Ceiling(srcHeight * scale);

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info)
            ?? throw new InvalidOperationException("Cannot allocate the code raster surface.");
        var canvas = surface.Canvas;

        // Keep the canvas clear so the SVG alone defines its background.
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Cannot encode the code raster as PNG.");
        return encoded.ToArray();
    }
}
