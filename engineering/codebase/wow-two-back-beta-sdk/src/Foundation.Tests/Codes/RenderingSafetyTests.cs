using System.Text;
using SkiaSharp;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Codes;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Codes;

public sealed class RenderingSafetyTests
{
    [Theory]
    [InlineData(BarcodeFormat.QrCode, "hello")]
    [InlineData(BarcodeFormat.DataMatrix, "hello")]
    [InlineData(BarcodeFormat.Pdf417, "hello")]
    [InlineData(BarcodeFormat.Aztec, "hello")]
    [InlineData(BarcodeFormat.Code128, "12345678")]
    [InlineData(BarcodeFormat.Ean13, "5901234123457")]
    [InlineData(BarcodeFormat.UpcA, "012345678905")]
    public void EverySymbology_HonorsPngAndSvg(BarcodeFormat format, string payload)
    {
        using var provider = new ServiceCollection().AddCodeRendering().BuildServiceProvider();
        var renderer = provider.GetRequiredService<ICodeRenderer>();
        foreach (var imageFormat in Enum.GetValues<ImageFormat>())
        {
            var output = renderer.Render(Request() with { Payload = payload, Symbology = format, Format = imageFormat }).ValueOrThrow();
            Assert.Equal(imageFormat, output.Format);
            Assert.Equal(imageFormat == ImageFormat.Png ? "image/png" : "image/svg+xml", output.ContentType);
            if (imageFormat == ImageFormat.Png)
            {
                Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, output.Content[..8]);
                using var bitmap = SKBitmap.Decode(output.Content);
                using var rgba = new SKBitmap(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
                using (var canvas = new SKCanvas(rgba)) canvas.DrawBitmap(bitmap, 0, 0);
                var reader = new ZXing.BarcodeReaderGeneric { Options = new ZXing.Common.DecodingOptions { TryHarder = true } };
                var decoded = reader.Decode(new ZXing.RGBLuminanceSource(rgba.GetPixelSpan().ToArray(), rgba.Width, rgba.Height, ZXing.RGBLuminanceSource.BitmapFormat.RGBA32));
                Assert.NotNull(decoded);
                // UPC-A and EAN-13 carry the same GTIN when the latter has its leading zero.
                Assert.Equal(payload, format == BarcodeFormat.UpcA && decoded.Text.Length == 13 ? decoded.Text[1..] : decoded.Text);
            }
            else Assert.Contains("<svg", Encoding.UTF8.GetString(output.Content));
        }
    }

    [Fact]
    public void Facade_ReturnsFieldErrorsForUnsupportedEnumsNullStylesAndInvalidPayloads()
    {
        using var provider = new ServiceCollection().AddCodeRendering().BuildServiceProvider();
        var renderer = provider.GetRequiredService<ICodeRenderer>();
        foreach (var request in new[]
        {
            Request() with { Symbology = (BarcodeFormat)99 }, Request() with { Format = (ImageFormat)99 },
            Request() with { Style = null! }, Request() with { Payload = "" },
            Request() with { Symbology = BarcodeFormat.Ean13, Payload = "invalid" },
            Request() with { Payload = new string('a', 8192) },
        })
            Assert.IsType<ValidationError>(Assert.IsType<Result<RenderedCode>.Failure>(renderer.Render(request)).Error);
    }

    [Fact]
    public void StyleRules_RejectInjectionExternalImagesAndUnboundedValues()
    {
        using var provider = new ServiceCollection().AddCodeRendering().BuildServiceProvider();
        var validator = provider.GetRequiredService<IValidator<StyleSpec>>();
        foreach (var style in new[]
        {
            StyleSpec.Default with { ForegroundColor = "#000000\" onload=\"evil" },
            StyleSpec.Default with { BackgroundColor = "url(https://attacker.invalid/paint)" },
            StyleSpec.Default with { QuietZoneModules = int.MaxValue },
            StyleSpec.Default with { Logo = new LogoSpec { DataUrl = "https://attacker.invalid/image", SizeRatio = 0.2 } },
            StyleSpec.Default with { Logo = new LogoSpec { DataUrl = "data:image/svg+xml;base64,AAAA", SizeRatio = 0.2 } },
            StyleSpec.Default with { Emoji = new EmojiSpec { Glyph = "x", SizeRatio = double.NaN } },
            StyleSpec.Default with { Gradient = new LinearGradientSpec { Angle = double.PositiveInfinity, Stops = [] } },
            StyleSpec.Default with { Gradient = new LinearGradientSpec { Angle = 0, Stops = [new GradientStopSpec { Color = "red\" onload=\"evil", Offset = 0 }] } },
            StyleSpec.Default with { Gradient = new RadialGradientSpec { Radius = 1, Stops = [new GradientStopSpec { Color = "#000000", Offset = double.NaN }] } },
        }) Assert.NotNull(validator.Validate(style));
    }

    [Theory]
    [InlineData("<not-svg />")]
    [InlineData("<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'>&x;</svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg' width='999999999' height='1'/>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><image href='https://attacker.invalid/a'/></svg>")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><style>@import url(https://attacker.invalid/a);</style></svg>")]
    public void Rasterizer_RejectsInvalidOrActiveSvgBeforeNativeRendering(string svg)
        => Assert.Throws<ValidationException>(() => new SkiaSvgRasterizer().ToPng(svg));

    [Fact]
    public void Rasterizer_BoundsTotalInlineImagePixels()
    {
        using var bitmap = new SKBitmap(1024, 1024);
        bitmap.Erase(SKColors.White);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var url = "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
        var images = string.Concat(Enumerable.Repeat($"<image width='1' height='1' href='{url}'/>", 16));
        Assert.Throws<ValidationException>(() => new SkiaSvgRasterizer().ToPng(
            $"<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'>{images}</svg>"));
    }

    [Fact]
    public void Rasterizer_RejectsUnboundedOutput()
        => Assert.Throws<ValidationException>(() => new SkiaSvgRasterizer().ToPng("<svg/>", int.MaxValue));

    private static CodeRenderRequest Request() => new()
    {
        Payload = "hello", Format = ImageFormat.Svg, Symbology = BarcodeFormat.QrCode, Style = StyleSpec.Default,
    };
}
