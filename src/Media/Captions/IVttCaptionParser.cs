namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Parses WebVTT caption content into ordered, cleaned <see cref="CaptionSegment"/> values.</summary>
public interface IVttCaptionParser
{
    /// <summary>
    /// Parses WebVTT content into an ordered collection of caption segments — stripping inline per-word
    /// timing and styling tags, decoding HTML entities, and de-duplicating rolling auto-caption overlays.
    /// </summary>
    /// <param name="vttContent">The raw WebVTT document text.</param>
    /// <returns>The parsed segments in document order; empty when the input is blank or has no cues.</returns>
    IReadOnlyList<CaptionSegment> Parse(string vttContent);
}
