namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents how hard a PDF is compressed: embedded JPEG photos are shrunk and re-encoded, streams deflated.</summary>
public sealed record PdfCompressSpec
{
    /// <summary>Gets the JPEG quality photos are re-encoded at, 1–100. Default 60.</summary>
    public int ImageQuality { get; init; } = 60;

    /// <summary>Gets the widest or tallest a photo may stay, in pixels. Default 1600.</summary>
    public int MaxImageDimension { get; init; } = 1600;
}
