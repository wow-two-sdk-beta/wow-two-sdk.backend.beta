namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Parses SubRip (<c>.srt</c>) subtitle content into ordered, cleaned <see cref="CaptionSegment"/> values.</summary>
public interface ISrtCaptionParser
{
    /// <summary>
    /// Parses SubRip content — numbered cue blocks separated by blank lines with comma-decimal timings —
    /// stripping inline markup and decoding HTML entities.
    /// </summary>
    /// <param name="srtContent">The raw SubRip document text.</param>
    /// <returns>The parsed segments in document order; empty when the input is blank or has no cues.</returns>
    IReadOnlyList<CaptionSegment> Parse(string srtContent);
}
