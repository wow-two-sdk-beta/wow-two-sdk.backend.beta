namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents the text of a PDF in reading order, per page and joined.</summary>
public sealed record PdfTextResult
{
    /// <summary>Gets the text of each page read, keyed by page number from 1.</summary>
    public required IReadOnlyDictionary<int, string> Pages { get; init; }

    /// <summary>Gets every page's text, joined with blank lines.</summary>
    public string Text => string.Join("\n\n", Pages.OrderBy(page => page.Key).Select(page => page.Value));
}
