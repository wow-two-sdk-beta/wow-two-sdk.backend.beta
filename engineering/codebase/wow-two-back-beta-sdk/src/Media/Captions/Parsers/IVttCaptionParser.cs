namespace WoW.Two.Sdk.Backend.Beta.Media.Captions.Parsers;

/// <summary>Defines WebVTT parsing into ordered, cleaned <see cref="CaptionSegment"/> values.</summary>
public interface IVttCaptionParser
{
    /// <summary>
    /// Parses WebVTT content into an ordered collection of caption segments — stripping inline per-word
    /// timing and styling tags, decoding HTML entities, and de-duplicating rolling auto-caption overlays.
    /// </summary>
    /// <param name="vttContent">The raw WebVTT document text.</param>
    /// <returns>The parsed segments in document order; empty when the input is blank or has no cues.</returns>
    /// <remarks>
    /// Null or blank input yields no segments. Header text is consumed through the first blank line or timing line.
    /// Timing lines require two-digit hours and three decimal digits. Unmatched or text-less cues are skipped, so incomplete input can yield partial cues.
    /// Matched but out-of-range timestamps throw instead of returning a partial list. Cue order and start/end relationships are not validated.
    /// Rolling auto-captions, known by per-word timing tags, yield each spoken line once; written captions yield
    /// one segment per cue, less an identical cue repeated at the previous end.
    /// </remarks>
    /// <exception cref="FormatException">A matched timestamp is invalid for the supported clock format.</exception>
    IReadOnlyList<CaptionSegment> Parse(string vttContent);
}
