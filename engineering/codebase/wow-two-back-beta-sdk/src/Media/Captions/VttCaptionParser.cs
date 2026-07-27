using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>
/// Default <see cref="IVttCaptionParser"/> — a minimal WebVTT parser that strips YouTube auto-caption
/// inline timing and styling tags, decodes HTML entities, and de-duplicates rolling overlays, emitting
/// plain-text segments. Stateless and thread-safe.
/// </summary>
public sealed partial class VttCaptionParser : IVttCaptionParser
{
    // Timing line: "00:00:01.234 --> 00:00:04.567" with optional trailing settings.
    [GeneratedRegex(@"^(\d{2}:\d{2}:\d{2}[.,]\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}[.,]\d{3})")]
    private static partial Regex TimingLine();

    // YouTube embeds per-word timing tags and styling — strip them all.
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex InlineTags();

    /// <inheritdoc />
    public IReadOnlyList<CaptionSegment> Parse(string vttContent)
    {
        if (string.IsNullOrWhiteSpace(vttContent)) return [];

        var segments = new List<CaptionSegment>();
        var lines = vttContent.Replace("\r\n", "\n").Split('\n');

        var i = 0;
        // Skip header (WEBVTT line + metadata until first blank line).
        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])) i++;

        TimeSpan? lastEnd = null;
        string? lastText = null;

        while (i < lines.Length)
        {
            // Skip blank lines + optional cue identifier line.
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i])) i++;
            if (i >= lines.Length) break;

            // If current line isn't a timing line, it's a cue id — skip one and try again.
            if (!TimingLine().IsMatch(lines[i]))
            {
                i++;
                if (i >= lines.Length) break;
            }

            var match = TimingLine().Match(lines[i]);
            if (!match.Success)
            {
                i++;
                continue;
            }
            i++;

            var start = ParseTimestamp(match.Groups[1].Value);
            var end = ParseTimestamp(match.Groups[2].Value);

            // Collect text lines until blank.
            var textBuilder = new StringBuilder();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                var cleaned = InlineTags().Replace(lines[i], string.Empty);
                cleaned = System.Net.WebUtility.HtmlDecode(cleaned).Trim();
                if (cleaned.Length > 0)
                {
                    if (textBuilder.Length > 0) textBuilder.Append(' ');
                    textBuilder.Append(cleaned);
                }
                i++;
            }

            var text = textBuilder.ToString().Trim();
            if (text.Length == 0) continue;

            // YouTube auto-captions emit each segment twice (rolling overlay effect):
            // dedupe by skipping if this segment is identical to the previous one
            // and starts where the previous ended.
            if (lastText == text && lastEnd is { } prevEnd && Math.Abs((start - prevEnd).TotalMilliseconds) < 50)
                continue;

            segments.Add(new CaptionSegment { Start = start, End = end, Text = text });
            lastEnd = end;
            lastText = text;
        }

        return segments;
    }

    private static TimeSpan ParseTimestamp(string ts)
    {
        // Normalize comma decimals (SRT-style) to dot.
        ts = ts.Replace(',', '.');
        return TimeSpan.ParseExact(ts, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }
}
