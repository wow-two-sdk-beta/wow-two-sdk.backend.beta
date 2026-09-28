namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents what a PDF holds: pages, their sizes, and its document information.</summary>
public sealed record PdfInfoResult
{
    /// <summary>Gets the pages.</summary>
    public required int PageCount { get; init; }

    /// <summary>Gets each page's size and rotation.</summary>
    public required IReadOnlyList<PdfPageInfo> Pages { get; init; }

    /// <summary>Gets whether the file is encrypted.</summary>
    public required bool IsEncrypted { get; init; }

    /// <summary>Gets the PDF version, such as <c>1.7</c>.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the document information: title, author, subject, keywords, creator, producer.</summary>
    public required PdfMetadataSpec Metadata { get; init; }
}
