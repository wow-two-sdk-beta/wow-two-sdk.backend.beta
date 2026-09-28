namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Refers to the paper a generated page uses.</summary>
public enum PdfPageSize
{
    /// <summary>A4, 210 × 297 mm.</summary>
    A4,

    /// <summary>US Letter, 8.5 × 11 in.</summary>
    Letter,

    /// <summary>US Legal, 8.5 × 14 in.</summary>
    Legal,

    /// <summary>A3, 297 × 420 mm.</summary>
    A3,

    /// <summary>A5, 148 × 210 mm.</summary>
    A5,

    /// <summary>The image's own size, one point per pixel at 72 DPI.</summary>
    Image,
}
