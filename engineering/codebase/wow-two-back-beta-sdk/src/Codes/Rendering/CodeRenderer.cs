using System.Text;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering;

/// <summary>Provides the facade that dispatches a render request to the QR or barcode renderer based on symbology and format.</summary>
public sealed class CodeRenderer(IQrCodeRenderer qr, IBarcodeRenderer barcode) : ICodeRenderer
{
    /// <inheritdoc />
    public RenderedCode Render(CodeRenderRequest request)
    {
        if (request.Symbology == BarcodeFormat.QrCode)
        {
            return request.Format == ImageFormat.Png
                ? new RenderedCode(qr.RenderPng(request.Payload, request.Style), "image/png", ImageFormat.Png)
                : new RenderedCode(Utf8(qr.RenderSvg(request.Payload, request.Style)), "image/svg+xml", ImageFormat.Svg);
        }

        // Barcodes render to plain SVG; route them through the rasterizer for PNG once that is wired.
        var svg = barcode.RenderSvg(request.Payload, request.Symbology, request.Style);
        return new RenderedCode(Utf8(svg), "image/svg+xml", ImageFormat.Svg);
    }

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);
}
