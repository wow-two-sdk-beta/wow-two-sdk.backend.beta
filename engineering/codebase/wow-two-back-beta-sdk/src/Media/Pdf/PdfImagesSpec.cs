namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents how images become PDF pages: one image per page, upright, centered inside the margins.</summary>
public sealed record PdfImagesSpec
{
    /// <summary>Gets the paper. Default <see cref="PdfPageSize.A4"/>.</summary>
    public PdfPageSize PageSize { get; init; } = PdfPageSize.A4;

    /// <summary>Gets whether a page turns landscape for a wide image. Default true.</summary>
    public bool AutoOrient { get; init; } = true;

    /// <summary>Gets the margin in points; ignored for <see cref="PdfPageSize.Image"/>. Default 36 (half an inch).</summary>
    public double Margin { get; init; } = 36;

    /// <summary>Gets the widest or tallest an embedded image may be in pixels; larger ones shrink. Default 2480 (A4 at 300 DPI).</summary>
    public int MaxImageDimension { get; init; } = 2480;

    /// <summary>Gets the JPEG quality of embedded photos. Default 85.</summary>
    public int ImageQuality { get; init; } = 85;
}
