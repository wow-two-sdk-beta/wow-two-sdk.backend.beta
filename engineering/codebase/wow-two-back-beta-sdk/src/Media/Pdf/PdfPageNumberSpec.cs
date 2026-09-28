using WoW.Two.Sdk.Backend.Beta.Media.Images;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents page numbers stamped on pages.</summary>
public sealed record PdfPageNumberSpec
{
    /// <summary>Gets the template: <c>{page}</c> is the number, <c>{total}</c> the last number. Default <c>{page} / {total}</c>.</summary>
    public string Format { get; init; } = "{page} / {total}";

    /// <summary>Gets where the number sits. Default <see cref="ImageAnchor.Bottom"/>.</summary>
    public ImageAnchor Anchor { get; init; } = ImageAnchor.Bottom;

    /// <summary>Gets the distance from the page edges in points. Default 24.</summary>
    public double Margin { get; init; } = 24;

    /// <summary>Gets the font family; null takes the configured default.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Gets the font size in points. Default 10.</summary>
    public double FontSize { get; init; } = 10;

    /// <summary>Gets the color as <c>#RRGGBB</c>. Default dark gray.</summary>
    public string Color { get; init; } = "#333333";

    /// <summary>Gets the number the first stamped page shows. Default 1.</summary>
    public int StartAt { get; init; } = 1;

    /// <summary>Gets the pages stamped; null stamps every page.</summary>
    public IReadOnlyList<PdfPageRange>? Pages { get; init; }
}
