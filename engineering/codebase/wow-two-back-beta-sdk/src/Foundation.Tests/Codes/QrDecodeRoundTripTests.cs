using Xunit;
using SkiaSharp;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Svg;
using ZXing;
using ZXing.Common;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Codes;

/// <summary>Proves a styled QR still decodes back to its payload.</summary>
public sealed class QrDecodeRoundTripTests
{
    private readonly QrCodeRenderer _renderer = new(
        new QrMatrixGenerator(),
        new SvgRenderer(),
        new SkiaSvgRasterizer());
    private const string Payload = "https://foreverpin.com/abc1234";

    public static TheoryData<ModuleShape, FinderShape, FinderDotShape> Styles() => new()
    {
        { ModuleShape.Square,        FinderShape.Square,  FinderDotShape.Square },
        { ModuleShape.Dots,          FinderShape.Rounded, FinderDotShape.Circle },
        { ModuleShape.Rounded,       FinderShape.Rounded, FinderDotShape.Rounded },
        { ModuleShape.Classy,        FinderShape.Circle,  FinderDotShape.Circle },
        { ModuleShape.ClassyRounded, FinderShape.Square,  FinderDotShape.Square },
        { ModuleShape.VerticalBars,  FinderShape.Rounded, FinderDotShape.Square },
        { ModuleShape.HorizontalBars,FinderShape.Square,  FinderDotShape.Rounded },
    };

    [Theory]
    [MemberData(nameof(Styles))]
    public void Styled_qr_still_decodes_to_its_payload(ModuleShape module, FinderShape finder, FinderDotShape dot)
    {
        // Start from a low ECC so the normalizer's stylised-module bump is exercised on the way through.
        var style = StyleSpec.Default with
        {
            EccLevel = EccLevel.L,
            ModuleShape = module,
            FinderShape = finder,
            FinderDotShape = dot,
        };

        var png = _renderer.RenderPng(Payload, style);
        var decoded = Decode(png);

        Assert.Equal(Payload, decoded);
    }

    [Fact]
    public void Gradient_qr_still_decodes_to_its_payload()
    {
        // A foreground gradient is a fill change only (geometry untouched) — proves Skia rasterizes the gradient AND it
        // scans.
        var style = StyleSpec.Default with
        {
            Gradient = new LinearGradientSpec
            {
                Angle = 45,
                Stops =
                [
                    new GradientStopSpec { Color = "#1a1a2e", Offset = 0 },
                    new GradientStopSpec { Color = "#16213e", Offset = 1 },
                ],
            },
        };

        Assert.Equal(Payload, Decode(_renderer.RenderPng(Payload, style)));
    }

    [Fact]
    public void Transparent_background_qr_still_decodes_to_its_payload()
    {
        // Transparent bg rasterizes with no background rect; the decoder flattens transparency to white so contrast
        // survives.
        var style = StyleSpec.Default with { TransparentBackground = true };

        Assert.Equal(Payload, Decode(_renderer.RenderPng(Payload, style)));
    }

    [Fact]
    public void Center_emoji_qr_still_decodes_to_its_payload()
    {
        // The emoji halo clears the center; the auto-bumped ECC=H reconstructs the occluded modules.
        var style = StyleSpec.Default with { Emoji = new EmojiSpec { Glyph = "🎉", SizeRatio = 0.25 } };

        Assert.Equal(Payload, Decode(_renderer.RenderPng(Payload, style)));
    }

    [Fact]
    public void Max_size_center_emoji_qr_still_decodes_to_its_payload()
    {
        // Worst case — the max-clamped emoji size (0.27). The halo + ECC=H must still leave the symbol readable.
        var style = StyleSpec.Default with { Emoji = new EmojiSpec { Glyph = "🔥", SizeRatio = 0.27 } };

        Assert.Equal(Payload, Decode(_renderer.RenderPng(Payload, style)));
    }

    /// <summary>Decodes a PNG QR back to its text with ZXing, or null when undecodable.</summary>
    private static string? Decode(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png)
            ?? throw new InvalidOperationException("Skia could not decode the rendered PNG.");

        // SKBitmap → tightly-packed RGBA8888 bytes for ZXing's RGBLuminanceSource.
        using var rgba = ToRgba(bitmap);

        var pixels = rgba.GetPixelSpan().ToArray();
        var source = new RGBLuminanceSource(pixels, rgba.Width, rgba.Height, RGBLuminanceSource.BitmapFormat.RGBA32);

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [ZXing.BarcodeFormat.QR_CODE],
            },
        };

        return reader.Decode(source)?.Text;
    }

    private static SKBitmap ToRgba(SKBitmap source)
    {
        var converted = new SKBitmap(new SKImageInfo(
            source.Width,
            source.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul));
        using var canvas = new SKCanvas(converted);
        canvas.Clear(SKColors.White); // flatten any transparency to white so contrast survives for the decoder
        canvas.DrawBitmap(source, 0, 0);
        return converted;
    }
}
