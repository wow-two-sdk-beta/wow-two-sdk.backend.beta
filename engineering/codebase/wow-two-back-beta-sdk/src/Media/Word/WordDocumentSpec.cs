namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Represents how a built document is titled and laid out; null fields take the configured defaults.</summary>
public sealed record WordDocumentSpec
{
    /// <summary>Gets the title property.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the author property.</summary>
    public string? Author { get; init; }

    /// <summary>Gets the body font; null takes <see cref="WordOptions.FontFamily"/>.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Gets the body font size in points; null takes <see cref="WordOptions.FontSizePoints"/>.</summary>
    public double? FontSizePoints { get; init; }

    /// <summary>Gets the paper. Default A4.</summary>
    public WordPaperSize Paper { get; init; } = WordPaperSize.A4;

    /// <summary>Gets the page margin on every side, in millimetres. Default 25.</summary>
    public double MarginMillimeters { get; init; } = 25;
}
