using System.Globalization;

namespace WoW.Two.Sdk.Backend.Beta.Media.Captions;

/// <summary>Shared timecode parsing across caption formats (VTT/SRT clock times, TTML clock + offset times).</summary>
internal static class CaptionTimecode
{
    /// <summary>
    /// Parses a clock timecode of the form <c>[hh:]mm:ss[.,]fff</c> (VTT/SRT). Accepts either a dot or a
    /// comma decimal separator and an optional hours field. Returns <see cref="TimeSpan.Zero"/> when unparseable.
    /// </summary>
    /// <param name="value">The timecode text.</param>
    /// <returns>The parsed offset, or zero when the input cannot be parsed.</returns>
    public static TimeSpan ParseClock(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TimeSpan.Zero;

        var normalized = value.Trim().Replace(',', '.');
        var parts = normalized.Split(':');

        try
        {
            return parts.Length switch
            {
                3 => new TimeSpan(0, int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), 0)
                     + SecondsWithFraction(parts[2]),
                2 => new TimeSpan(0, 0, int.Parse(parts[0], CultureInfo.InvariantCulture), 0)
                     + SecondsWithFraction(parts[1]),
                _ => TimeSpan.Zero,
            };
        }
        catch (FormatException)
        {
            return TimeSpan.Zero;
        }
        catch (OverflowException)
        {
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Parses a TTML time expression — either a clock time (<c>hh:mm:ss.fff</c>) or an offset with a metric
    /// suffix (<c>1.5s</c>, <c>1500ms</c>, <c>90000t</c> ticks unsupported → zero). Returns zero when unparseable.
    /// </summary>
    /// <param name="value">The TTML time expression.</param>
    /// <returns>The parsed offset, or zero when the input cannot be parsed.</returns>
    public static TimeSpan ParseTtml(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TimeSpan.Zero;

        var text = value.Trim();
        if (text.Contains(':', StringComparison.Ordinal))
            return ParseClock(text);

        if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            return TryDouble(text[..^2], out var ms) ? TimeSpan.FromMilliseconds(ms) : TimeSpan.Zero;

        if (text.EndsWith('s'))
            return TryDouble(text[..^1], out var s) ? TimeSpan.FromSeconds(s) : TimeSpan.Zero;

        return TryDouble(text, out var seconds) ? TimeSpan.FromSeconds(seconds) : TimeSpan.Zero;
    }

    /// <summary>
    /// Formats an offset as an <c>hh:mm:ss</c> clock timecode with a 3-digit fractional part, using
    /// <paramref name="fractionSeparator"/> (<c>'.'</c> for WebVTT, <c>','</c> for SubRip). Hours are not
    /// capped at 24 so long-form media formats correctly.
    /// </summary>
    /// <param name="offset">The offset to format (negative values are clamped to zero).</param>
    /// <param name="fractionSeparator">The decimal separator between seconds and milliseconds.</param>
    /// <returns>The formatted timecode, e.g. <c>01:02:03.400</c>.</returns>
    public static string FormatClock(TimeSpan offset, char fractionSeparator)
    {
        if (offset < TimeSpan.Zero) offset = TimeSpan.Zero;
        var hours = (int)offset.TotalHours;
        return string.Create(CultureInfo.InvariantCulture, $"{hours:D2}:{offset.Minutes:D2}:{offset.Seconds:D2}{fractionSeparator}{offset.Milliseconds:D3}");
    }

    private static TimeSpan SecondsWithFraction(string secondsField)
    {
        var seconds = double.Parse(secondsField, CultureInfo.InvariantCulture);
        return TimeSpan.FromSeconds(seconds);
    }

    private static bool TryDouble(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
