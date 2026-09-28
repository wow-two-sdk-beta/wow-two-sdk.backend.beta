namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Represents the text extracted from a Word document.</summary>
public sealed record WordTextResult
{
    /// <summary>Gets the text, in the requested format.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the format of <see cref="Text"/>.</summary>
    public required WordTextFormat Format { get; init; }

    /// <summary>Gets the words in the body text.</summary>
    public required int Words { get; init; }
}
