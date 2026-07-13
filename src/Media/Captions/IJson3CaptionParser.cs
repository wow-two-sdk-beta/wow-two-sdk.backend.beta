namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Parses YouTube <c>json3</c> timed-text into ordered, cleaned <see cref="CaptionSegment"/> values.</summary>
public interface IJson3CaptionParser
{
    /// <summary>
    /// Parses YouTube <c>json3</c> content — <c>events[]</c> each carrying <c>tStartMs</c>, <c>dDurationMs</c>,
    /// and <c>segs[].utf8</c> — concatenating segment runs and skipping window-definition events with no text.
    /// </summary>
    /// <param name="json3Content">The raw <c>json3</c> document text.</param>
    /// <returns>The parsed segments in document order; empty when the input is blank, malformed, or text-less.</returns>
    IReadOnlyList<CaptionSegment> Parse(string json3Content);
}
