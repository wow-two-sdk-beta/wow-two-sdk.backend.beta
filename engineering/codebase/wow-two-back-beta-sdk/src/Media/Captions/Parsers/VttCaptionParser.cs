using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions.Parsers;

/// <summary>
/// Parses WebVTT caption text into plain-text segments, stripping inline tags and decoding HTML entities.
/// Reads YouTube's rolling auto-captions line by line, so each spoken line appears once.
/// </summary>
/// <remarks>
/// Rolling captions — recognized by their per-word timing tags — repeat the previous line above each new one and
/// flash the finished line alone between cues; each segment keeps only the lines its cue added. Written captions
/// keep one segment per cue, dropping an identical cue repeated at the previous cue's end.
/// </remarks>
public sealed partial class VttCaptionParser : IVttCaptionParser
{
    // Timing line: "00:00:01.234 --> 00:00:04.567" with optional trailing settings.
    [GeneratedRegex(@"^(\d{2}:\d{2}:\d{2}[.,]\d{3})\s*-->\s*(\d{2}:\d{2}:\d{2}[.,]\d{3})")]
    private static partial Regex TimingLine();

    // YouTube embeds per-word timing tags and styling — strip them all.
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex InlineTags();

    // A per-word timing tag, such as "<00:00:01.280>" — only rolling auto-captions carry them.
    [GeneratedRegex(@"<\d{2}:\d{2}:\d{2}[.,]\d{3}>")]
    private static partial Regex WordTiming();

    /// <inheritdoc />
    public IReadOnlyList<CaptionSegment> Parse(string vttContent)
    {
        if (string.IsNullOrWhiteSpace(vttContent)) return [];

        var cues = ReadCues(vttContent);
        return WordTiming().IsMatch(vttContent) ? Unroll(cues) : Merge(cues);
    }

    /// <summary>Reads every cue — its timing and its cleaned, non-empty text lines.</summary>
    private static List<Cue> ReadCues(string vttContent)
    {
        var lines = vttContent.Replace("\r\n", "\n").Split('\n');
        var cues = new List<Cue>();

        var i = 0;
        // Skip header (WEBVTT line + metadata until first blank line).
        while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]) && !TimingLine().IsMatch(lines[i])) i++;

        while (i < lines.Length)
        {
            // Blank lines, cue identifiers and NOTE or STYLE blocks sit between cues.
            var match = TimingLine().Match(lines[i]);
            i++;
            if (!match.Success) continue;

            var start = ParseTimestamp(match.Groups[1].Value);
            var end = ParseTimestamp(match.Groups[2].Value);

            // A cue's text runs to the first empty line; YouTube opens some cues with a line holding one space.
            var text = new List<string>();
            while (i < lines.Length && lines[i].Length > 0 && !TimingLine().IsMatch(lines[i]))
            {
                var cleaned = WebUtility.HtmlDecode(InlineTags().Replace(lines[i], string.Empty)).Trim();
                if (cleaned.Length > 0) text.Add(cleaned);
                i++;
            }

            cues.Add(new Cue(start, end, text));
        }

        return cues;
    }

    /// <summary>Maps written captions to one segment per cue.</summary>
    private static List<CaptionSegment> Merge(List<Cue> cues)
    {
        var segments = new List<CaptionSegment>();
        TimeSpan? lastEnd = null;
        string? lastText = null;

        foreach (var cue in cues)
        {
            var text = string.Join(' ', cue.Lines);
            if (text.Length == 0) continue;

            // Drop YouTube's duplicate rolling-overlay segment at the prior boundary.
            if (lastText == text && lastEnd is { } prevEnd && Math.Abs((cue.Start - prevEnd).TotalMilliseconds) < 50)
                continue;

            segments.Add(new CaptionSegment { Start = cue.Start, End = cue.End, Text = text });
            lastEnd = cue.End;
            lastText = text;
        }

        return segments;
    }

    /// <summary>Maps rolling captions to one segment per cue that adds a line, holding only the added lines.</summary>
    private static List<CaptionSegment> Unroll(List<Cue> cues)
    {
        var segments = new List<CaptionSegment>();
        IReadOnlyList<string> previous = [];

        foreach (var cue in cues)
        {
            if (cue.Lines.Count == 0) continue;

            var added = cue.Lines.Skip(Overlap(previous, cue.Lines)).ToList();
            previous = cue.Lines;
            if (added.Count == 0) continue;

            segments.Add(new CaptionSegment { Start = cue.Start, End = cue.End, Text = string.Join(' ', added) });
        }

        return segments;
    }

    /// <summary>Counts the most lines a cue opens with that close the previous cue.</summary>
    private static int Overlap(IReadOnlyList<string> previous, IReadOnlyList<string> current)
    {
        for (var count = Math.Min(previous.Count, current.Count); count > 0; count--)
        {
            if (previous.Skip(previous.Count - count).SequenceEqual(current.Take(count), StringComparer.Ordinal))
                return count;
        }

        return 0;
    }

    private static TimeSpan ParseTimestamp(string ts)
    {
        // Normalize comma decimals (SRT-style) to dot.
        ts = ts.Replace(',', '.');
        return TimeSpan.ParseExact(ts, @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
    }

    /// <summary>Represents one cue — when it shows and its text lines.</summary>
    private sealed record Cue(TimeSpan Start, TimeSpan End, IReadOnlyList<string> Lines);
}
