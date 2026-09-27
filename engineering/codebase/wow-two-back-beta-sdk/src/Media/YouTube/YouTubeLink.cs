namespace WoW.Two.Sdk.Backend.Beta.Media.YouTube;

/// <summary>Represents one YouTube link found in pasted text.</summary>
public sealed record YouTubeLink
{
    /// <summary>Gets the piece of text as it was pasted.</summary>
    public required string Input { get; init; }

    /// <summary>Gets what the link names.</summary>
    public required YouTubeLinkKind Kind { get; init; }

    /// <summary>Gets the video or playlist identifier.</summary>
    public required string Id { get; init; }
}
