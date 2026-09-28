using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Media.Markdown;

/// <summary>
/// Provides <see cref="IMarkdownService"/> over Markdig with the advanced extensions and YAML front matter. Raw HTML is
/// escaped unless allowed; link and image URLs outside http, https, mailto, tel, relative paths and data images become
/// <c>#</c>, so a <c>javascript:</c> link cannot run.
/// </summary>
/// <param name="options">Raw HTML, link treatment and reading speed; <c>Media:Markdown</c> reloads live.</param>
public sealed partial class MarkdigMarkdownService(IOptionsMonitor<MarkdownOptions> options) : IMarkdownService
{
    private static readonly MarkdownPipeline Safe = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().DisableHtml().Build();
    private static readonly MarkdownPipeline Trusted = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().Build();
    private static readonly string[] SafeSchemes = ["http", "https", "mailto", "tel"];

    /// <inheritdoc />
    public MarkdownResult Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var current = options.CurrentValue;
        var pipeline = current.AllowRawHtml ? Trusted : Safe;
        var document = Markdig.Markdown.Parse(markdown, pipeline);

        foreach (var link in document.Descendants<LinkInline>())
            link.Url = Guard(link.Url, link.IsImage, current, link);
        foreach (var link in document.Descendants<AutolinkInline>())
            link.Url = Guard(link.Url, image: false, current, link) ?? "#";

        var text = ToPlainText(markdown);
        var words = Words().Count(text);
        return new MarkdownResult
        {
            Html = Markdig.Markdown.ToHtml(document, pipeline),
            Text = text,
            Headings = [.. document.Descendants<HeadingBlock>().Select(heading => new MarkdownHeading
            {
                Level = heading.Level,
                Text = InlineText(heading.Inline),
                Id = heading.GetAttributes().Id ?? string.Empty,
            })],
            FrontMatter = FrontMatter(document),
            WordCount = words,
            ReadingMinutes = words == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(words / (double)Math.Max(1, current.WordsPerMinute))),
        };
    }

    /// <inheritdoc />
    public string ToPlainText(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Markdig.Markdown.ToPlainText(markdown, Safe).Trim();
    }

    /// <summary>The URL when its scheme is safe, else <c>#</c>; external links gain the configured <c>rel</c> and <c>target</c>.</summary>
    private static string? Guard(string? url, bool image, MarkdownOptions current, IMarkdownObject link)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        var compact = new string([.. url.Where(character => !char.IsWhiteSpace(character) && !char.IsControl(character))]);
        var colon = compact.IndexOf(':', StringComparison.Ordinal);
        var slash = compact.IndexOfAny(['/', '?', '#']);
        if (colon < 0 || (slash >= 0 && slash < colon))
            return url;

        var scheme = compact[..colon].ToLowerInvariant();
        if (image && scheme == "data" && compact.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return url;
        if (!SafeSchemes.Contains(scheme))
            return "#";

        if (!image && scheme is "http" or "https")
        {
            var attributes = link.GetAttributes();
            if (current.NoFollowExternalLinks)
                attributes.AddPropertyIfNotExist("rel", "nofollow noopener noreferrer");
            if (current.ExternalLinksNewTab)
                attributes.AddPropertyIfNotExist("target", "_blank");
        }

        return url;
    }

    private static string InlineText(ContainerInline? inline)
    {
        var text = new StringBuilder();
        if (inline is null)
            return string.Empty;

        foreach (var item in inline.Descendants<LeafInline>())
        {
            switch (item)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
            }
        }

        return text.ToString().Trim();
    }

    /// <summary>The front matter's top-level <c>key: value</c> pairs; nested structures are not read.</summary>
    private static Dictionary<string, string> FrontMatter(MarkdownDocument document)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (document.Descendants<YamlFrontMatterBlock>().FirstOrDefault() is not { } block)
            return values;

        foreach (var line in block.Lines.ToString().Split('\n'))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0 || char.IsWhiteSpace(line[0]) || line.TrimStart().StartsWith('#'))
                continue;

            var value = line[(separator + 1)..].Trim();
            if (value.StartsWith('[') && value.EndsWith(']'))
                value = string.Join(", ", value[1..^1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Unquote));
            values[line[..separator].Trim()] = Unquote(value);
        }

        return values;
    }

    private static string Unquote(string value)
        => value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')) ? value[1..^1] : value;

    [GeneratedRegex(@"\w+(['’-]\w+)*")]
    private static partial Regex Words();
}
