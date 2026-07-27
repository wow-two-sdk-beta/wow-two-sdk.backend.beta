namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Parses TTML / DFXP caption XML into ordered, cleaned <see cref="CaptionSegment"/> values.</summary>
public interface ITtmlCaptionParser
{
    /// <summary>
    /// Parses TTML (Timed Text Markup Language) / DFXP content — <c>&lt;p begin= end=|dur=&gt;</c> cues under
    /// any namespace — flattening inline spans, treating <c>&lt;br/&gt;</c> as a space, and collapsing whitespace.
    /// </summary>
    /// <param name="ttmlContent">The raw TTML/DFXP document text.</param>
    /// <returns>The parsed segments in document order; empty when the input is blank, malformed, or cue-less.</returns>
    IReadOnlyList<CaptionSegment> Parse(string ttmlContent);
}
