using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>
/// Default <see cref="ISrtCaptionParser"/> — a resilient SubRip parser that tolerates missing or
/// out-of-order index lines, blank-line-separated cues, comma or dot decimals, and CRLF/LF line endings,
/// stripping inline tags (<c>&lt;i&gt;</c>, <c>&lt;font&gt;</c>, …) and decoding HTML entities.
/// Stateless and thread-safe.
/// </summary>
public sealed partial class SrtCaptionParser : ISrtCaptionParser
{
    [GeneratedRegex(@"(\d{1,2}:\d{2}:\d{2}[.,]\d{3})\s*-->\s*(\d{1,2}:\d{2}:\d{2}[.,]\d{3})")]
    private static partial Regex TimingLine();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex InlineTags();

    /// <inheritdoc />
    public IReadOnlyList<CaptionSegment> Parse(string srtContent)
    {
        if (string.IsNullOrWhiteSpace(srtContent)) return [];

        var segments = new List<CaptionSegment>();
        var normalized = srtContent.Replace("\r\n", "\n").Replace('\r', '\n');
        var blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n');

            var timingIndex = Array.FindIndex(lines, line => TimingLine().IsMatch(line));
            if (timingIndex < 0) continue;

            var timing = TimingLine().Match(lines[timingIndex]);
            var start = CaptionTimecode.ParseClock(timing.Groups[1].Value);
            var end = CaptionTimecode.ParseClock(timing.Groups[2].Value);

            var text = new StringBuilder();
            for (var j = timingIndex + 1; j < lines.Length; j++)
            {
                var cleaned = WebUtility.HtmlDecode(InlineTags().Replace(lines[j], string.Empty)).Trim();
                if (cleaned.Length == 0) continue;
                if (text.Length > 0) text.Append(' ');
                text.Append(cleaned);
            }

            var value = text.ToString().Trim();
            if (value.Length > 0)
                segments.Add(new CaptionSegment { Start = start, End = end, Text = value });
        }

        return segments;
    }
}
