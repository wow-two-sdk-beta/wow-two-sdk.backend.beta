using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Media.YouTube;

/// <summary>Maps YouTube URLs in any published form to video and playlist identifiers, and identifiers back to canonical URLs.</summary>
public static class YouTubeUrlMapper
{
    /// <summary>Maps a video identifier to its canonical watch URL, opening at a second when one is given.</summary>
    /// <param name="videoId">The YouTube video identifier.</param>
    /// <param name="at">The offset the video opens at, or <c>null</c> for its start.</param>
    /// <returns>The canonical <c>watch?v=</c> URL, with <c>&amp;t={seconds}s</c> when an offset is given.</returns>
    public static string ToWatchUrl(string videoId, TimeSpan? at = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        var url = YouTubeUrlConstants.WatchUrlPrefix + videoId;
        return at is { } offset
            ? string.Create(CultureInfo.InvariantCulture, $"{url}&t={Math.Max(0, (long)offset.TotalSeconds)}s")
            : url;
    }

    /// <summary>Maps a video URL to its canonical watch URL.</summary>
    /// <param name="url">The video URL as a person supplied it.</param>
    /// <returns>The canonical watch URL, or the trimmed input when it names no YouTube video.</returns>
    public static string ToCanonicalUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return TryGetVideoId(url, out var videoId) ? ToWatchUrl(videoId) : url.Trim();
    }

    /// <summary>Reads the video identifier from a watch, short, embed, shorts or live URL.</summary>
    /// <param name="url">The video URL as a person supplied it.</param>
    /// <param name="videoId">The video identifier, or an empty string when none was found.</param>
    /// <returns><c>true</c> when the URL names a YouTube video; otherwise <c>false</c>.</returns>
    public static bool TryGetVideoId(string? url, out string videoId)
    {
        videoId = string.Empty;
        if (url is null || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var candidate = host == YouTubeUrlConstants.ShortLinkHost
            ? segments.FirstOrDefault()
            : YouTubeUrlConstants.Hosts.Contains(host) ? ReadVideoId(uri, segments) : null;

        if (candidate is null || !IsVideoId(candidate))
            return false;

        videoId = candidate;
        return true;
    }

    /// <summary>Maps a playlist identifier to its canonical playlist URL.</summary>
    /// <param name="playlistId">The YouTube playlist identifier.</param>
    /// <returns>The canonical <c>playlist?list=</c> URL of the playlist.</returns>
    public static string ToPlaylistUrl(string playlistId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        return YouTubeUrlConstants.PlaylistUrlPrefix + playlistId;
    }

    /// <summary>Reads the playlist identifier from a playlist page URL.</summary>
    /// <remarks>
    /// A watch URL that also carries <c>list=</c> names one video, so only <c>/playlist</c> pages count. Mixes
    /// (<c>RD…</c>) are endless radio feeds and never count as playlists.
    /// </remarks>
    /// <param name="url">The playlist URL as a person supplied it.</param>
    /// <param name="playlistId">The playlist identifier, or an empty string when none was found.</param>
    /// <returns><c>true</c> when the URL names an expandable YouTube playlist; otherwise <c>false</c>.</returns>
    public static bool TryGetPlaylistId(string? url, out string playlistId)
    {
        playlistId = string.Empty;
        if (url is null || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (!YouTubeUrlConstants.Hosts.Contains(uri.Host.ToLowerInvariant()) || segments is not ["playlist"])
            return false;

        var candidate = ReadQuery(uri, "list");
        if (candidate is null || !IsPlaylistId(candidate))
            return false;

        playlistId = candidate;
        return true;
    }

    /// <summary>Reads the video identifier from a youtube.com path or query.</summary>
    /// <param name="uri">The parsed URL.</param>
    /// <param name="segments">The non-empty path segments of the URL.</param>
    /// <returns>The candidate identifier, or <c>null</c> when the URL carries none.</returns>
    private static string? ReadVideoId(Uri uri, string[] segments)
    {
        if (segments is ["watch"])
            return ReadQuery(uri, "v");

        return segments is ["embed" or "shorts" or "live" or "v", var id, ..] ? id : null;
    }

    /// <summary>Reads one query parameter of a URL.</summary>
    /// <param name="uri">The parsed URL.</param>
    /// <param name="name">The parameter name.</param>
    /// <returns>The first value of the parameter, or <c>null</c> when the URL does not carry it.</returns>
    private static string? ReadQuery(Uri uri, string name)
    {
        var prefix = name + "=";
        return uri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(pair => pair.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    }

    /// <summary>Checks the URL-safe shape of a playlist identifier, excluding endless mixes.</summary>
    /// <param name="candidate">The identifier read from the URL.</param>
    /// <returns><c>true</c> when the candidate names an expandable playlist; otherwise <c>false</c>.</returns>
    private static bool IsPlaylistId(string candidate)
        => candidate.Length is >= YouTubeUrlConstants.MinPlaylistIdLength and <= YouTubeUrlConstants.MaxPlaylistIdLength
           && !candidate.StartsWith(YouTubeUrlConstants.MixPlaylistPrefix, StringComparison.Ordinal)
           && candidate.All(IsUrlSafe);

    /// <summary>Checks the eleven-character URL-safe base64 shape of a video identifier.</summary>
    /// <param name="candidate">The identifier read from the URL.</param>
    /// <returns><c>true</c> when the candidate has the identifier shape; otherwise <c>false</c>.</returns>
    private static bool IsVideoId(string candidate)
        => candidate.Length == YouTubeUrlConstants.VideoIdLength && candidate.All(IsUrlSafe);

    /// <summary>Checks one character of URL-safe base64.</summary>
    /// <param name="character">The character.</param>
    /// <returns><c>true</c> for letters, digits, <c>-</c> and <c>_</c>.</returns>
    private static bool IsUrlSafe(char character) => char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
}
