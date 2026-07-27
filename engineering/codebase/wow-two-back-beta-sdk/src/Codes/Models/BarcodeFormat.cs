namespace WoW.Two.Sdk.Backend.Beta.Codes.Models;

/// <summary>Concrete symbology the engine renders. QR is the default; the rest cover the "all code types" surface.</summary>
/// <remarks>
/// This is the SDK's <em>rendering</em> vocabulary. A consuming product keeps its own persisted/domain
/// format enum and maps into this at the render boundary — the SDK never owns a persisted product column.
/// </remarks>
public enum BarcodeFormat
{
    /// <summary>QR code — 2D, the default.</summary>
    QrCode,

    /// <summary>Data Matrix — 2D, industrial / pharma / small-part marking, GS1.</summary>
    DataMatrix,

    /// <summary>PDF417 — 2D stacked, IDs / tickets / boarding passes.</summary>
    Pdf417,

    /// <summary>Aztec — 2D, transport tickets (robust at low resolution, no quiet zone).</summary>
    Aztec,

    /// <summary>Code 128 — 1D, logistics / asset labels.</summary>
    Code128,

    /// <summary>EAN-13 — 1D retail product barcode.</summary>
    Ean13,

    /// <summary>UPC-A — 1D retail product barcode.</summary>
    UpcA,
}
