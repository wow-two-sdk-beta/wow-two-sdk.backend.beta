namespace WoW.Two.Sdk.Backend.Beta.Media.Markdown;

/// <summary>Holds how Markdown renders: raw HTML, link treatment and reading speed.</summary>
/// <remarks>Set in code with <c>AddMarkdown(o => …)</c> or in the host section <c>Media:Markdown</c>, which is applied last.</remarks>
public sealed record MarkdownOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Media:Markdown";

    /// <summary>Gets or sets whether raw HTML in the source passes through; off escapes it, which user content needs. Default false.</summary>
    public bool AllowRawHtml { get; set; }

    /// <summary>Gets or sets whether absolute http(s) links open in a new tab. Default true.</summary>
    public bool ExternalLinksNewTab { get; set; } = true;

    /// <summary>Gets or sets whether absolute http(s) links carry <c>rel="nofollow noopener noreferrer"</c>. Default true.</summary>
    public bool NoFollowExternalLinks { get; set; } = true;

    /// <summary>Gets or sets the reading speed behind <see cref="MarkdownResult.ReadingMinutes"/>. Default 200 words per minute.</summary>
    public int WordsPerMinute { get; set; } = 200;
}
