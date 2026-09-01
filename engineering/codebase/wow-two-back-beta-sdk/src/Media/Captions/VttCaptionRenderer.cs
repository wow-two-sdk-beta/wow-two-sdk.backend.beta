using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Writes caption segments as a WebVTT (<c>.vtt</c>) document. Stateless and thread-safe.</summary>
public sealed class VttCaptionRenderer : ICaptionRenderer
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
            builder.Append(CaptionTimecodeMapper.FormatClock(segment.Start, '.'))
                   .Append(" --> ")
                   .Append(CaptionTimecodeMapper.FormatClock(segment.End, '.'))
                   .Append('\n')
                   .Append(segment.Text)
                   .Append("\n\n");
        }

        return builder.ToString();
    }
}
