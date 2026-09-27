namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Renders timed text as plain text — timestamped lines to scan, or paragraphs to read.</summary>
public static class TimedTextRenderer
{
    /// <summary>Renders spans as <c>[m:ss] text</c> lines, or as blank-line-separated paragraphs without timestamps.</summary>
    /// <typeparam name="T">The span type.</typeparam>
    /// <param name="spans">The ordered spans.</param>
    /// <param name="options">Timestamps on or off, and what breaks paragraphs; defaults when <c>null</c>.</param>
    /// <returns>The text, ending with a newline; empty when there are no spans.</returns>
    public static string Write<T>(IReadOnlyList<T> spans, TimedTextOptions? options = null)
        where T : ITimedText
    {
        ArgumentNullException.ThrowIfNull(spans);
        options ??= new TimedTextOptions();
        if (spans.Count == 0) return string.Empty;

        return options.HasTimestamps
            ? string.Join('\n', spans.Select(span => $"[{CaptionClockMapper.ToClock(span.Start)}] {span.Text}")) + "\n"
            : string.Join("\n\n", spans.ToParagraphs(options)) + "\n";
    }
}
