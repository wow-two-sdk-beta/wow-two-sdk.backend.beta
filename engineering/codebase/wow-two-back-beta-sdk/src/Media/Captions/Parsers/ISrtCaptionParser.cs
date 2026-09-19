namespace WoW.Two.Sdk.Backend.Beta.Media.Captions.Parsers;

/// <summary>Defines SubRip (<c>.srt</c>) parsing into ordered, cleaned <see cref="CaptionSegment"/> values.</summary>
public interface ISrtCaptionParser
{
    /// <summary>
    /// Parses SubRip content — numbered cue blocks separated by blank lines with comma-decimal timings —
    /// stripping inline markup and decoding HTML entities.
    /// </summary>
    /// <param name="srtContent">The raw SubRip document text.</param>
    /// <returns>The parsed segments in document order; empty when the input is blank or has no cues.</returns>
    /// <remarks>
    /// Null or blank input yields no segments. Blank-line-separated blocks without a matching timing line
    /// or nonempty text are skipped, so malformed or incomplete input may produce only accepted cues.
    /// Matching times use one or two hour digits and three decimal digits; unparseable or overflowing conversions return zero.
    /// Clock-component ranges, cue order and duration relationships are not strictly validated.
    /// </remarks>
    IReadOnlyList<CaptionSegment> Parse(string srtContent);
}
