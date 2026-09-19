using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using ZXing.Common;
using WoW.Two.Sdk.Backend.Beta.Codes.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering;

/// <summary>Renders barcodes through a ZXing.Net-backed <see cref="IBarcodeRenderer"/> that emits plain managed SVG.</summary>
public sealed class BarcodeRenderer : IBarcodeRenderer
{
    /// <inheritdoc />
    public string RenderSvg(string payload, BarcodeFormat format, StyleSpec style)
    {
        // Barcodes render plain, so the style is not consumed yet.
        style.ValidateForRendering();
        if (string.IsNullOrEmpty(payload) || payload.Length > 8192)
            throw CodeRenderValidationExtensions.Invalid("Payload", "Use a nonempty payload of at most 8192 characters.");

        var writer = new ZXing.BarcodeWriterSvg
        {
            Format = MapFormat(format),
            Options = new EncodingOptions
            {
                Width = 300,
                Height = IsOneDimensional(format) ? 120 : 300,
                Margin = 4,
                PureBarcode = false,
            },
        };

        try { return Emit(writer.Encode(payload), IsOneDimensional(format) ? payload : null); }
        catch (XmlException) { throw CodeRenderValidationExtensions.Invalid("Payload", "Payload cannot be represented as XML text."); }
        catch (ArgumentException) { throw CodeRenderValidationExtensions.Invalid("Payload", "Payload cannot be encoded in the selected symbology."); }
    }

    private static string Emit(BitMatrix matrix, string? caption)
    {
        var width = matrix.Width;
        var height = matrix.Height + (caption is null ? 0 : 13);
        if (width > 4096 || height > 4096 || (long)width * height > 16_000_000)
            throw CodeRenderValidationExtensions.Invalid("Payload", "Barcode dimensions exceed the rendering budget.");
        var path = new StringBuilder();
        for (var y = 0; y < matrix.Height; y++)
        {
            for (var x = 0; x < width;)
            {
                if (!matrix[x, y]) { x++; continue; }
                var start = x;
                while (x < width && matrix[x, y]) x++;
                path.Append(CultureInfo.InvariantCulture, $"M{start} {y}h{x - start}v1h-{x - start}z");
            }
        }
        XNamespace ns = "http://www.w3.org/2000/svg";
        var svg = new XElement(ns + "svg", new XAttribute("width", width), new XAttribute("height", height),
            new XAttribute("viewBox", FormattableString.Invariant($"0 0 {width} {height}")), new XAttribute("shape-rendering", "crispEdges"),
            new XElement(ns + "rect", new XAttribute("width", width), new XAttribute("height", height), new XAttribute("fill", "#FFFFFF")),
            new XElement(ns + "path", new XAttribute("fill", "#000000"), new XAttribute("d", path.ToString())));
        if (caption is not null)
        {
            // Control characters can be valid barcode data, but cannot be XML caption text.
            var text = new string(caption.Where(c => !char.IsControl(c)).ToArray());
            XmlConvert.VerifyXmlChars(text);
            svg.Add(new XElement(ns + "text", new XAttribute("x", "50%"), new XAttribute("y", "98%"),
                new XAttribute("font-family", "Arial"), new XAttribute("font-size", "10"), new XAttribute("text-anchor", "middle"), text));
        }
        return svg.ToString(SaveOptions.DisableFormatting);
    }

    private static ZXing.BarcodeFormat MapFormat(BarcodeFormat format) => format switch
    {
        BarcodeFormat.QrCode => ZXing.BarcodeFormat.QR_CODE,
        BarcodeFormat.DataMatrix => ZXing.BarcodeFormat.DATA_MATRIX,
        BarcodeFormat.Pdf417 => ZXing.BarcodeFormat.PDF_417,
        BarcodeFormat.Aztec => ZXing.BarcodeFormat.AZTEC,
        BarcodeFormat.Code128 => ZXing.BarcodeFormat.CODE_128,
        BarcodeFormat.Ean13 => ZXing.BarcodeFormat.EAN_13,
        BarcodeFormat.UpcA => ZXing.BarcodeFormat.UPC_A,
        _ => throw CodeRenderValidationExtensions.Invalid("Symbology", "Unsupported barcode symbology."),
    };

    private static bool IsOneDimensional(BarcodeFormat format) => format
        is BarcodeFormat.Code128 or BarcodeFormat.Ean13 or BarcodeFormat.UpcA;
}
