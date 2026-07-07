namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Represents a single timed line of caption text within a <see cref="CaptionTrack"/>.</summary>
public sealed record CaptionSegment
{
    /// <summary>Gets the start offset of the segment from the media start.</summary>
    public required TimeSpan Start { get; init; }

    /// <summary>Gets the end offset of the segment from the media start.</summary>
    public required TimeSpan End { get; init; }

    /// <summary>Gets the caption text for the segment, with inline markup stripped and HTML entities decoded.</summary>
    public required string Text { get; init; }
}
