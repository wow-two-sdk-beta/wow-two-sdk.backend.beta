namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>
/// Defines behavior that reads, fills and builds Word (.docx) documents. Inputs over the configured limit, not a Word
/// document, or a template missing values raise <see cref="WordRejectedException"/>.
/// </summary>
public interface IWordService
{
    /// <summary>Reads the document properties and counts the paragraphs, tables and images.</summary>
    /// <param name="docx">The document.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<WordInfoResult> ReadInfoAsync(Stream docx, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the body text in reading order: paragraphs and tables, tracked deletions left out. Markdown keeps
    /// headings, lists, emphasis, links and tables, a shape language models and search indexes take well.
    /// </summary>
    /// <param name="docx">The document.</param>
    /// <param name="format">Plain text or Markdown.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<WordTextResult> ExtractTextAsync(Stream docx, WordTextFormat format = WordTextFormat.Plain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills <c>{{path}}</c> placeholders in the body, headers and footers — even where Word split them across runs —
    /// keeping each placeholder's formatting. A table row holding <c>{{Items[].Field}}</c> repeats once per item.
    /// </summary>
    /// <param name="template">The template document.</param>
    /// <param name="spec">The values and the treatment of missing ones.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<WordResult> FillTemplateAsync(Stream template, WordTemplateSpec spec, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a document from Markdown: headings, paragraphs, emphasis, code, links, lists, quotes, tables and rules,
    /// as real Word styles and numbering, so the result edits like a hand-made document.
    /// </summary>
    /// <param name="markdown">The Markdown source.</param>
    /// <param name="spec">Title, author, font and page; null takes the configured defaults.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<WordResult> FromMarkdownAsync(string markdown, WordDocumentSpec? spec = null, CancellationToken cancellationToken = default);
}
