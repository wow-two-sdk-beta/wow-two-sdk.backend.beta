namespace WoW.Two.Sdk.Backend.Beta.Media.YouTube;

/// <summary>Represents the YouTube links found in pasted text, and the pieces that named none.</summary>
public sealed record YouTubeLinkExtraction
{
    /// <summary>Gets the unique links, in the order they were pasted.</summary>
    public required IReadOnlyList<YouTubeLink> Links { get; init; }

    /// <summary>Gets the pasted pieces that named no YouTube video or playlist, in paste order.</summary>
    public required IReadOnlyList<string> Unrecognized { get; init; }
}
