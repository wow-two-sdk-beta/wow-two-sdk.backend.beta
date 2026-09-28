namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents one page's size and rotation.</summary>
public sealed record PdfPageInfo
{
    /// <summary>Gets the page number, from 1.</summary>
    public required int Number { get; init; }

    /// <summary>Gets the width in points (1/72 inch), before rotation.</summary>
    public required double Width { get; init; }

    /// <summary>Gets the height in points, before rotation.</summary>
    public required double Height { get; init; }

    /// <summary>Gets the clockwise rotation viewers apply: 0, 90, 180 or 270.</summary>
    public required int Rotation { get; init; }
}
