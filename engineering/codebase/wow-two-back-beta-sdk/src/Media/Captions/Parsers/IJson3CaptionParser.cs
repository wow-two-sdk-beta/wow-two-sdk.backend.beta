namespace WoW.Two.Sdk.Backend.Beta.Media.Captions.Parsers;

/// <summary>Defines YouTube <c>json3</c> parsing into ordered <see cref="CaptionSegment"/> values.</summary>
public interface IJson3CaptionParser
{
    /// <summary>
    /// Parses YouTube <c>json3</c> content — <c>events[]</c> each carrying <c>tStartMs</c>, <c>dDurationMs</c>,
    /// and <c>segs[].utf8</c> — concatenating segment runs and skipping window-definition events with no text.
    /// </summary>
    /// <param name="json3Content">The raw <c>json3</c> document text.</param>
    /// <returns>The accepted segments in document order; empty for blank input, invalid JSON syntax or no accepted text.</returns>
    /// <remarks>
    /// A missing or non-array events property yields no segments. Events without a start or segment array, or without text, are skipped.
    /// Numeric starts not representable as Int64 are skipped; missing durations or numeric durations not representable as Int64 default to zero.
    /// Invalid JSON syntax returns no partial data. Wrong JSON kinds and out-of-range times can throw without returning a partial list.
    /// Text runs are concatenated and whitespace collapsed; markup and HTML entities are preserved.
    /// </remarks>
    /// <exception cref="InvalidOperationException">An expected object or numeric field has an incompatible JSON kind.</exception>
    /// <exception cref="OverflowException">A time is outside the supported TimeSpan range.</exception>
    IReadOnlyList<CaptionSegment> Parse(string json3Content);
}
