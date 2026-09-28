namespace WoW.Two.Sdk.Backend.Beta.Media.Markdown;

/// <summary>Represents rendered Markdown: its HTML, plain text, headings, front matter and length.</summary>
public sealed record MarkdownResult
{
    /// <summary>Gets the HTML; raw HTML is escaped unless allowed, and unsafe link schemes are neutralized.</summary>
    public required string Html { get; init; }

    /// <summary>Gets the text without markup, for search indexes and previews.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the headings in document order.</summary>
    public required IReadOnlyList<MarkdownHeading> Headings { get; init; }

    /// <summary>Gets the front matter's top-level keys; a list reads as its items joined with commas.</summary>
    public required IReadOnlyDictionary<string, string> FrontMatter { get; init; }

    /// <summary>Gets the words in the text.</summary>
    public required int WordCount { get; init; }

    /// <summary>Gets the minutes the text takes to read, at least 1 when there is any text.</summary>
    public required int ReadingMinutes { get; init; }
}
