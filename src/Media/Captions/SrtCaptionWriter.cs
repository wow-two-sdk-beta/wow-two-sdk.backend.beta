using System.Globalization;
using System.Text;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Writes caption segments as a SubRip (<c>.srt</c>) document with 1-based cue numbering. Stateless and thread-safe.</summary>
public sealed class SrtCaptionWriter : ICaptionWriter
{
    /// <inheritdoc />
    public CaptionFormat Format => CaptionFormat.SubRip;

    /// <inheritdoc />
    public string Write(IReadOnlyList<CaptionSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var builder = new StringBuilder();
        var index = 1;

        foreach (var segment in segments)
        {
            builder.Append(index.ToString(CultureInfo.InvariantCulture)).Append('\n')
                   .Append(CaptionTimecode.FormatClock(segment.Start, ','))
                   .Append(" --> ")
                   .Append(CaptionTimecode.FormatClock(segment.End, ','))
                   .Append('\n')
                   .Append(segment.Text)
                   .Append("\n\n");
            index++;
        }

        return builder.ToString();
    }
}
