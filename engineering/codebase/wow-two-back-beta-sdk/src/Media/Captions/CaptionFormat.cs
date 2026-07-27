namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>The caption/subtitle serialization formats the SDK can parse.</summary>
public enum CaptionFormat
{
    /// <summary>WebVTT (<c>.vtt</c>) — the web standard; <c>WEBVTT</c> header, <c>hh:mm:ss.mmm</c> timings.</summary>
    WebVtt,

    /// <summary>SubRip (<c>.srt</c>) — numbered cue blocks, comma-decimal <c>hh:mm:ss,mmm</c> timings.</summary>
    SubRip,

    /// <summary>TTML / DFXP (<c>.ttml</c>, <c>.dfxp</c>) — XML with <c>&lt;p begin= end=&gt;</c> cues.</summary>
    Ttml,

    /// <summary>YouTube <c>json3</c> — <c>{ "events": [ { tStartMs, dDurationMs, segs:[{ utf8 }] } ] }</c>.</summary>
    YouTubeJson3,
}
