using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Writes caption segments as a WebVTT (<c>.vtt</c>) document. Stateless and thread-safe.</summary>
public sealed class VttCaptionWriter : ICaptionWriter
{
    /// <inheritdoc />
    public CaptionFormat Format => CaptionFormat.WebVtt;

    /// <inheritdoc />
    public string Write(IReadOnlyList<CaptionSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var builder = new StringBuilder();
        builder.Append("WEBVTT\n\n");

        foreach (var segment in segments)
        {
            builder.Append(CaptionTimecode.FormatClock(segment.Start, '.'))
                   .Append(" --> ")
                   .Append(CaptionTimecode.FormatClock(segment.End, '.'))
                   .Append('\n')
                   .Append(segment.Text)
                   .Append("\n\n");
        }

        return builder.ToString();
    }
}
