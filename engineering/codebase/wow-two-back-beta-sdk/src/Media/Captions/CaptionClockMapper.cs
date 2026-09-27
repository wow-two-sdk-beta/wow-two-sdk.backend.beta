using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Maps media offsets to the clock people read beside a line of text.</summary>
public static class CaptionClockMapper
{
    /// <summary>Maps an offset to the clock YouTube shows — <c>m:ss</c>, or <c>h:mm:ss</c> from the first hour on.</summary>
    /// <param name="offset">The offset from the media start; negative values read as zero, fractions are dropped.</param>
    /// <returns>The clock, e.g. <c>0:05</c>, <c>12:40</c> or <c>1:02:03</c>.</returns>
    public static string ToClock(TimeSpan offset)
    {
        if (offset < TimeSpan.Zero) offset = TimeSpan.Zero;
        return offset.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)offset.TotalHours}:{offset.Minutes:00}:{offset.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{(int)offset.TotalMinutes}:{offset.Seconds:00}");
    }
}
