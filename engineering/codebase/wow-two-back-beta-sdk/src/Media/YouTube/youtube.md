# WoW.Two.Sdk.Backend.Beta.Media.YouTube

> Pure YouTube link handling — every published URL form to video and playlist identifiers, canonical URLs back, and
> links found in pasted text. No network, no external dependencies.

Part of the `WoW2.Sdk.Backend.Beta` mono-lib (folder `src/Media/YouTube/`). Extracted from TranscriptForge v0.5.

## Usage

```csharp
using WoW.Two.Sdk.Backend.Beta.Media.YouTube;

YouTubeUrlMapper.TryGetVideoId("https://youtu.be/dQw4w9WgXcQ?si=x", out var videoId);   // "dQw4w9WgXcQ"
YouTubeUrlMapper.ToWatchUrl(videoId, TimeSpan.FromSeconds(65));  // "https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=65s"

var found = YouTubeLinkExtractor.Extract(pastedText);
foreach (var link in found.Links)
{
    // link.Kind == Video → one video; Playlist → expand it (yt-dlp --flat-playlist, the Data API, …)
}
// found.Unrecognized — the pieces that named no link, to show back to the person
```

## Types

| Type | Role |
|---|---|
| `YouTubeUrlMapper` | `TryGetVideoId` · `TryGetPlaylistId` · `ToWatchUrl` (optional start offset) · `ToPlaylistUrl` · `ToCanonicalUrl` |
| `YouTubeLinkExtractor` | `Extract(text)` → unique links in paste order plus the unrecognized pieces |
| `YouTubeLink` · `YouTubeLinkKind` · `YouTubeLinkExtraction` | The extracted link, what it names, the whole result |
| `YouTubeUrlConstants` | Hosts, URL prefixes and identifier shapes |

## Behavior

- Video forms: `watch?v=`, `youtu.be/`, `embed/`, `shorts/`, `live/`, `v/`, on youtube.com, m., music. and
  youtube-nocookie.com hosts. Video ids are eleven URL-safe base64 characters.
- Playlists count only on `/playlist?list=` pages; a watch URL's `list=` stays one video, and mixes (`RD…`) never
  count.
- Extraction splits on whitespace and commas, trims Markdown and quote wrappers, and keeps each link once.
