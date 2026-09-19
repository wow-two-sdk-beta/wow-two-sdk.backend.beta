namespace WoW.Two.Sdk.Backend.Beta.Media.Captions.Parsers;

/// <summary>Defines TTML / DFXP XML parsing into ordered, cleaned <see cref="CaptionSegment"/> values.</summary>
public interface ITtmlCaptionParser
{
    /// <summary>
    /// Parses TTML (Timed Text Markup Language) / DFXP content — <c>&lt;p begin= end=|dur=&gt;</c> cues under
    /// any namespace — flattening inline spans, treating <c>&lt;br/&gt;</c> as a space, and collapsing whitespace.
    /// </summary>
    /// <param name="ttmlContent">The raw TTML/DFXP document text.</param>
    /// <returns>The accepted segments in document order; empty for blank input, invalid XML syntax or no accepted cues.</returns>
    /// <remarks>
    /// Null or blank input and invalid XML syntax yield no segments. Paragraphs without a begin attribute or nonempty text are skipped.
    /// Missing end and duration use the start time as the end. Unsupported or unparseable time tokens usually normalize to zero.
    /// Nonfinite or out-of-range numeric offsets and duration arithmetic can throw, with no partial list returned.
    /// Cue ordering is not validated. XML text and entities follow XML parsing; arbitrary inline elements are flattened rather than rendered.
    /// </remarks>
    /// <exception cref="ArgumentException">A numeric time value is NaN.</exception>
    /// <exception cref="OverflowException">A time or computed end is outside the supported TimeSpan range.</exception>
    IReadOnlyList<CaptionSegment> Parse(string ttmlContent);
}
