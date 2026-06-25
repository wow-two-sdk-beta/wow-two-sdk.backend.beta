using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using ZXing.Common;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering;

/// <summary>Provides a ZXing.Net-backed <see cref="IBarcodeRenderer"/> that emits plain managed SVG.</summary>
public sealed class BarcodeRenderer : IBarcodeRenderer
{
    /// <inheritdoc />
    public string RenderSvg(string payload, BarcodeFormat format, StyleSpec style)
    {
        // Barcodes render plain, so the style is not consumed yet.
        _ = style;

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

        return writer.Write(payload).ToString();
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
        _ => ZXing.BarcodeFormat.CODE_128,
    };

    private static bool IsOneDimensional(BarcodeFormat format) => format
        is BarcodeFormat.Code128 or BarcodeFormat.Ean13 or BarcodeFormat.UpcA;
}
