namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents text stamped across pages, such as DRAFT or CONFIDENTIAL.</summary>
public sealed record PdfWatermarkSpec
{
    /// <summary>Gets the text.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the font family; null takes the configured default, then Helvetica-like sans.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Gets the font size in points. Default 60.</summary>
    public double FontSize { get; init; } = 60;

    /// <summary>Gets the color as <c>#RRGGBB</c>. Default red.</summary>
    public string Color { get; init; } = "#FF0000";

    /// <summary>Gets the opacity, 0–1. Default 0.25.</summary>
    public double Opacity { get; init; } = 0.25;

    /// <summary>Gets the counter-clockwise angle in degrees. Default 45, along the diagonal.</summary>
    public double Angle { get; init; } = 45;

    /// <summary>Gets the pages stamped; null stamps every page.</summary>
    public IReadOnlyList<PdfPageRange>? Pages { get; init; }
}
