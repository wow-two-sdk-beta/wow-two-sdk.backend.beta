using System.Text;
using QRCoder.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;
using WoW.Two.Sdk.Backend.Beta.Codes.Validators;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering;

/// <summary>Renders code images by dispatching a render request to the QR or barcode renderer based on symbology and format.</summary>
public sealed class CodeRenderer(IQrCodeRenderer qr, IBarcodeRenderer barcode, ISvgRasterizer rasterizer) : ICodeRenderer
{
    /// <inheritdoc />
    public Result<RenderedCode> Render(CodeRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try { return Result<RenderedCode>.Ok(RenderValidated(request)); }
        catch (ValidationException exception) { return Result<RenderedCode>.Fail(exception.ValidationError); }
        catch (DataTooLongException)
        {
            return Result<RenderedCode>.Fail(CodeRenderValidationExtensions.Invalid("Payload", "Payload exceeds the selected QR capacity.").ValidationError);
        }
    }

    private RenderedCode RenderValidated(CodeRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.ValidateForRendering();

        if (request.Symbology == BarcodeFormat.QrCode)
        {
            return request.Format == ImageFormat.Png
                ? new RenderedCode { Content = qr.RenderPng(request.Payload, request.Style), ContentType = "image/png", Format = ImageFormat.Png }
                : new RenderedCode { Content = Utf8(qr.RenderSvg(request.Payload, request.Style)), ContentType = "image/svg+xml", Format = ImageFormat.Svg };
        }

        var svg = barcode.RenderSvg(request.Payload, request.Symbology, request.Style);
        return request.Format == ImageFormat.Png
            ? new RenderedCode { Content = rasterizer.ToPng(svg), ContentType = "image/png", Format = ImageFormat.Png }
            : new RenderedCode { Content = Utf8(svg), ContentType = "image/svg+xml", Format = ImageFormat.Svg };
    }

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);
}
