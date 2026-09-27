namespace WoW.Two.Sdk.Backend.Beta.Media.YouTube;

/// <summary>Holds the URL shapes YouTube serves videos and playlists under.</summary>
public static class YouTubeUrlConstants
{
    /// <summary>Holds the canonical watch URL prefix a video identifier is appended to.</summary>
    public const string WatchUrlPrefix = "https://www.youtube.com/watch?v=";

    /// <summary>Holds the canonical playlist URL prefix a playlist identifier is appended to.</summary>
    public const string PlaylistUrlPrefix = "https://www.youtube.com/playlist?list=";

    /// <summary>Holds the prefix of an endless mix, which never counts as a playlist.</summary>
    public const string MixPlaylistPrefix = "RD";

    /// <summary>Holds the shortest playlist identifier YouTube issues.</summary>
    public const int MinPlaylistIdLength = 10;

    /// <summary>Holds the longest playlist identifier YouTube issues.</summary>
    public const int MaxPlaylistIdLength = 64;

    /// <summary>Holds the short-link host whose first path segment is the video identifier.</summary>
    public const string ShortLinkHost = "youtu.be";

    /// <summary>Holds the length of a YouTube video identifier.</summary>
    public const int VideoIdLength = 11;

    /// <summary>Holds the host names that serve YouTube watch, embed, shorts and playlist pages.</summary>
    public static readonly IReadOnlyList<string> Hosts =
    [
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com",
        "youtube-nocookie.com",
        "www.youtube-nocookie.com",
    ];
}
