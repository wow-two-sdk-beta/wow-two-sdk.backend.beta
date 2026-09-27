namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Holds how timed text renders as plain text.</summary>
public sealed record TimedTextOptions
{
    /// <summary>Gets whether each line starts with its <c>[m:ss]</c> clock; without it, spans join into paragraphs. Default true.</summary>
    public bool HasTimestamps { get; init; } = true;

    /// <summary>Gets the silence between spans that starts a new paragraph. Default two seconds.</summary>
    public TimeSpan ParagraphPause { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets the speech length after which a paragraph breaks at the next span. Default one minute.</summary>
    public TimeSpan ParagraphLength { get; init; } = TimeSpan.FromMinutes(1);
}
