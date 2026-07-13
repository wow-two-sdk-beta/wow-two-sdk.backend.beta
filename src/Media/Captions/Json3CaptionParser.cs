using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>
/// Default <see cref="IJson3CaptionParser"/> — parses YouTube's <c>json3</c> caption payload. Emits one
/// segment per event that carries text, joining <c>segs[].utf8</c> runs, collapsing whitespace, and
/// skipping the window/position-only events YouTube interleaves. Returns empty on malformed JSON rather
/// than throwing. Stateless and thread-safe.
/// </summary>
public sealed partial class Json3CaptionParser : IJson3CaptionParser
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRuns();

    /// <inheritdoc />
    public IReadOnlyList<CaptionSegment> Parse(string json3Content)
    {
        if (string.IsNullOrWhiteSpace(json3Content)) return [];

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json3Content);
        }
        catch (JsonException)
        {
            return [];
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
                return [];

            var segments = new List<CaptionSegment>();

            foreach (var evt in events.EnumerateArray())
            {
                if (!evt.TryGetProperty("tStartMs", out var startElement) || !startElement.TryGetInt64(out var startMs))
                    continue;
                if (!evt.TryGetProperty("segs", out var segs) || segs.ValueKind != JsonValueKind.Array)
                    continue;

                var durationMs = evt.TryGetProperty("dDurationMs", out var durElement) && durElement.TryGetInt64(out var dur)
                    ? dur
                    : 0;

                var builder = new StringBuilder();
                foreach (var seg in segs.EnumerateArray())
                    if (seg.TryGetProperty("utf8", out var utf8) && utf8.ValueKind == JsonValueKind.String)
                        builder.Append(utf8.GetString());

                var text = WhitespaceRuns().Replace(builder.ToString(), " ").Trim();
                if (text.Length == 0)
                    continue;

                segments.Add(new CaptionSegment
                {
                    Start = TimeSpan.FromMilliseconds(startMs),
                    End = TimeSpan.FromMilliseconds(startMs + durationMs),
                    Text = text,
                });
            }

            return segments;
        }
    }
}
