namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Extends ordered timed text with the part a reader asks for and the paragraphs it reads as.</summary>
public static class TimedTextExtensions
{
    /// <summary>Keeps the spans that overlap a part of the media, in order; a span crossing either edge stays whole.</summary>
    /// <typeparam name="T">The span type.</typeparam>
    /// <param name="spans">The ordered spans.</param>
    /// <param name="start">The offset the part starts at.</param>
    /// <param name="end">The offset the part ends at.</param>
    /// <returns>The overlapping spans; empty when the part is empty or nothing overlaps it.</returns>
    public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> spans, TimeSpan start, TimeSpan end)
        where T : ITimedText
    {
        ArgumentNullException.ThrowIfNull(spans);
        return end <= start ? [] : spans.Where(span => span.End > start && span.Start < end).ToList();
    }

    /// <summary>Joins spans into paragraphs, breaking at pauses and after about a paragraph's length of speech.</summary>
    /// <typeparam name="T">The span type.</typeparam>
    /// <param name="spans">The ordered spans.</param>
    /// <param name="options">The pause and length that break paragraphs; defaults when <c>null</c>.</param>
    /// <returns>The paragraphs' text, in order.</returns>
    public static IReadOnlyList<string> ToParagraphs<T>(this IReadOnlyList<T> spans, TimedTextOptions? options = null)
        where T : ITimedText
    {
        ArgumentNullException.ThrowIfNull(spans);
        options ??= new TimedTextOptions();

        var paragraphs = new List<string>();
        var paragraph = new List<string>();
        TimeSpan? paragraphStart = null;
        TimeSpan? previousEnd = null;

        foreach (var span in spans)
        {
            var paused = previousEnd is { } end && span.Start - end >= options.ParagraphPause;
            var longEnough = paragraphStart is { } start && span.Start - start >= options.ParagraphLength;
            if (paragraph.Count > 0 && (paused || longEnough))
            {
                paragraphs.Add(string.Join(' ', paragraph));
                paragraph.Clear();
                paragraphStart = null;
            }

            paragraphStart ??= span.Start;
            paragraph.Add(span.Text);
            previousEnd = span.End;
        }

        if (paragraph.Count > 0)
            paragraphs.Add(string.Join(' ', paragraph));

        return paragraphs;
    }
}
