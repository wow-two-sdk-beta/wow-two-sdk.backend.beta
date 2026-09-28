namespace WoW.Two.Sdk.Backend.Beta.Media.Markdown;

/// <summary>Represents a heading, for a table of contents.</summary>
public sealed record MarkdownHeading
{
    /// <summary>Gets the level, 1–6.</summary>
    public required int Level { get; init; }

    /// <summary>Gets the heading text without markup.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the anchor id the rendered heading carries, for <c>#id</c> links.</summary>
    public required string Id { get; init; }
}
