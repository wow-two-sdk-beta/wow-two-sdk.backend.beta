namespace WoW.Two.Sdk.Backend.Beta.Media.YouTube;

/// <summary>Finds YouTube video and playlist links in pasted text — one per line, or separated by spaces or commas.</summary>
public static class YouTubeLinkExtractor
{
    /// <summary>Holds the characters that separate pasted pieces.</summary>
    private static readonly char[] Separators = [' ', '\t', '\r', '\n', ','];

    /// <summary>Holds the characters trimmed from each piece, as left around links by Markdown or prose.</summary>
    private static readonly char[] Wrappers = ['<', '>', '(', ')', '[', ']', '"', '\''];

    /// <summary>Extracts the links from pasted text, keeping each video or playlist once, in paste order.</summary>
    /// <remarks>
    /// A piece that is both a playlist page and a video is read as the playlist; a watch URL carrying <c>list=</c>
    /// stays one video. Expanding a playlist into its videos needs the network and is left to the caller.
    /// </remarks>
    /// <param name="text">The pasted text.</param>
    /// <returns>The unique links and the pieces that named no link.</returns>
    public static YouTubeLinkExtraction Extract(string? text)
    {
        var links = new List<YouTubeLink>();
        var unrecognized = new List<string>();
        var seen = new HashSet<(YouTubeLinkKind, string)>();

        foreach (var piece in (text ?? string.Empty).Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var input = piece.Trim(Wrappers);
            if (input.Length == 0)
                continue;

            if (YouTubeUrlMapper.TryGetPlaylistId(input, out var playlistId))
            {
                if (seen.Add((YouTubeLinkKind.Playlist, playlistId)))
                    links.Add(new YouTubeLink { Input = input, Kind = YouTubeLinkKind.Playlist, Id = playlistId });
                continue;
            }

            if (YouTubeUrlMapper.TryGetVideoId(input, out var videoId))
            {
                if (seen.Add((YouTubeLinkKind.Video, videoId)))
                    links.Add(new YouTubeLink { Input = input, Kind = YouTubeLinkKind.Video, Id = videoId });
                continue;
            }

            unrecognized.Add(input);
        }

        return new YouTubeLinkExtraction { Links = links, Unrecognized = unrecognized };
    }
}
