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
    /// Null or blank input yields no segments. Header text is consumed through the first blank line; headerless input can lose its first cue.
    /// Timing lines require two-digit hours and three decimal digits. Unmatched or text-less cues are skipped, so incomplete input can yield partial cues.
    /// Matched but out-of-range timestamps throw instead of returning a partial list. Cue order and start/end relationships are not validated.
    /// Adjacent duplicate rolling overlays are omitted.
    /// </remarks>
    /// <exception cref="FormatException">A matched timestamp is invalid for the supported clock format.</exception>
    IReadOnlyList<CaptionSegment> Parse(string vttContent);
}
