namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>
/// Serializes caption segments back into a text format. Pairing a parser with a writer of a different
/// format performs conversion (e.g. YouTube json3 → SubRip).
/// </summary>
public interface ICaptionWriter
{
    /// <summary>Gets the format this writer emits.</summary>
    CaptionFormat Format { get; }

    /// <summary>Serializes the segments into this writer's format.</summary>
    /// <param name="segments">The ordered segments to write.</param>
    /// <returns>The serialized caption document.</returns>
    string Write(IReadOnlyList<CaptionSegment> segments);
}
