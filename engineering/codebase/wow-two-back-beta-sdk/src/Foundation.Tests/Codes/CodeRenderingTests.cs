using Xunit;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Svg;
// Render tests target the SDK engine's symbology enum (not the persisted domain one).
using BarcodeFormat = WoW.Two.Sdk.Backend.Beta.Codes.Models.BarcodeFormat;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Codes;

/// <summary>Proves the generation library produces valid SVG and PNG for QR and barcodes.</summary>
public class CodeRenderingTests
{
    private readonly CodeRenderer _renderer = new(
        new QrCodeRenderer(new QrMatrixGenerator(), new SvgRenderer(), new SkiaSvgRasterizer()),
        new BarcodeRenderer(), new SkiaSvgRasterizer());

    [Fact]
    public void Qr_svg_is_vector_markup()
    {
        var result = _renderer.Render(new CodeRenderRequest
        {
            Payload = "https://foreverpin.com/abc1234",
            Symbology = BarcodeFormat.QrCode,
            Format = ImageFormat.Svg,
            Style = StyleSpec.Default,
        }).ValueOrThrow();

        Assert.Equal("image/svg+xml", result.ContentType);
        Assert.Contains("<svg", result.AsText());
    }

    [Fact]
    public void Qr_png_has_png_signature()
    {
        var result = _renderer.Render(new CodeRenderRequest
        {
            Payload = "https://foreverpin.com/abc1234",
            Symbology = BarcodeFormat.QrCode,
            Format = ImageFormat.Png,
            Style = StyleSpec.Default,
        }).ValueOrThrow();

        Assert.Equal("image/png", result.ContentType);
        Assert.True(result.Content.Length > 0);
        // PNG magic number: 89 50 4E 47
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, result.Content[..4]);
    }

    [Fact]
    public void Barcode_renders_to_svg()
    {
        var result = _renderer.Render(new CodeRenderRequest
        {
            Payload = "12345678",
            Symbology = BarcodeFormat.Code128,
            Format = ImageFormat.Svg,
            Style = StyleSpec.Default,
        }).ValueOrThrow();

        Assert.Equal("image/svg+xml", result.ContentType);
        Assert.Contains("<svg", result.AsText());
    }

    [Fact]
    public void Default_style_svg_is_byte_for_byte_identical_to_qrcoder()
    {
        // The default renderer must match the QRCoder reference output exactly.
        const string payload = "https://foreverpin.com/abc1234";

        var emitted = _renderer.Render(new CodeRenderRequest
        {
            Payload = payload,
            Symbology = BarcodeFormat.QrCode,
            Format = ImageFormat.Svg,
            Style = StyleSpec.Default,
        }).ValueOrThrow().AsText();

        var qrCoder = new QrCodeReferenceRenderer().Svg(payload);

        Assert.Equal(qrCoder, emitted);
    }
}
