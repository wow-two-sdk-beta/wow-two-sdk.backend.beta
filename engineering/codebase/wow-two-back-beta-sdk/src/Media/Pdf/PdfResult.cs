namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>Represents a PDF produced by an operation.</summary>
public sealed record PdfResult
{
    /// <summary>Gets the file bytes.</summary>
    public required byte[] Content { get; init; }

    /// <summary>Gets the pages.</summary>
    public required int PageCount { get; init; }

    /// <summary>Gets the media type, <c>application/pdf</c>.</summary>
    public string ContentType => "application/pdf";
}
