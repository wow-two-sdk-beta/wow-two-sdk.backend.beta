namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>
/// Defines behavior that reads, rearranges, protects, stamps and builds PDFs. Inputs over the configured limits, locked
/// without the right password, or asked for pages they lack raise <see cref="PdfRejectedException"/>.
/// </summary>
public interface IPdfService
{
    /// <summary>Reads the pages, their sizes, the version, encryption and document information.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="password">The open password, for an encrypted file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfInfoResult> ReadInfoAsync(Stream pdf, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>Joins <paramref name="pdfs"/> into one, in order.</summary>
    /// <param name="pdfs">The PDFs.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> MergeAsync(IReadOnlyList<Stream> pdfs, CancellationToken cancellationToken = default);

    /// <summary>Cuts the PDF into parts of <paramref name="pagesPerPart"/> pages; the last part may be shorter.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="pagesPerPart">Pages per part, from 1.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<IReadOnlyList<PdfResult>> SplitAsync(Stream pdf, int pagesPerPart, CancellationToken cancellationToken = default);

    /// <summary>Builds a PDF of the listed pages in the listed order — a subset, a reordering or both.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="pages">The pages, such as <c>PdfPageRange.Parse("3,1-2")</c>.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> ExtractPagesAsync(Stream pdf, IReadOnlyList<PdfPageRange> pages, CancellationToken cancellationToken = default);

    /// <summary>Drops the listed pages; dropping every page is refused.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="pages">The pages to drop.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> RemovePagesAsync(Stream pdf, IReadOnlyList<PdfPageRange> pages, CancellationToken cancellationToken = default);

    /// <summary>Turns pages clockwise by <paramref name="degrees"/>, a multiple of 90.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="degrees">The clockwise turn.</param>
    /// <param name="pages">The pages turned; null turns every page.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> RotatePagesAsync(Stream pdf, int degrees, IReadOnlyList<PdfPageRange>? pages = null, CancellationToken cancellationToken = default);

    /// <summary>Builds a PDF with one upright image per page.</summary>
    /// <param name="images">The encoded images, in order.</param>
    /// <param name="spec">Paper, margins and embedded image size.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> FromImagesAsync(IReadOnlyList<Stream> images, PdfImagesSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Sets the document information fields that are not null.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="metadata">The fields to set.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> UpdateMetadataAsync(Stream pdf, PdfMetadataSpec metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shrinks the PDF: embedded JPEG photos are downscaled and re-encoded (never grown), content streams deflated.
    /// Other image kinds (PNG-like, masks, indexed colors) stay as they are.
    /// </summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="spec">Photo quality and size.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> CompressAsync(Stream pdf, PdfCompressSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Protects the PDF with AES-256 and the given passwords and permissions.</summary>
    /// <param name="pdf">The PDF, unencrypted.</param>
    /// <param name="spec">Passwords and permissions.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> EncryptAsync(Stream pdf, PdfEncryptSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Removes the protection, keeping pages, outlines and document information.</summary>
    /// <param name="pdf">The encrypted PDF.</param>
    /// <param name="password">The owner password, or the open password.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> DecryptAsync(Stream pdf, string password, CancellationToken cancellationToken = default);

    /// <summary>Reads the text of the listed pages in reading order; scanned pages without a text layer read empty.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="pages">The pages read; null reads every page.</param>
    /// <param name="password">The open password, for an encrypted file.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfTextResult> ExtractTextAsync(Stream pdf, IReadOnlyList<PdfPageRange>? pages = null, string? password = null, CancellationToken cancellationToken = default);

    /// <summary>Stamps text across the listed pages, such as DRAFT.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="spec">The text, its look and the pages.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> AddWatermarkAsync(Stream pdf, PdfWatermarkSpec spec, CancellationToken cancellationToken = default);

    /// <summary>Stamps page numbers on the listed pages.</summary>
    /// <param name="pdf">The PDF.</param>
    /// <param name="spec">The template, position, look and pages.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<PdfResult> AddPageNumbersAsync(Stream pdf, PdfPageNumberSpec spec, CancellationToken cancellationToken = default);
}
