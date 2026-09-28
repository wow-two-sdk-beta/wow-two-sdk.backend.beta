using System.Globalization;
using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Maps <c>#RRGGBB[AA]</c> strings to Skia colors and back.</summary>
internal static class ImageColorMapper
{
    public static SKColor Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var hex = value.Trim().TrimStart('#');
        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
            throw new ImageRejectedException("image_color_invalid", $"'{value}' is not a #RRGGBB or #RRGGBBAA color.");

        return hex.Length == 6
            ? new SKColor((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed)
            : new SKColor((byte)(packed >> 24), (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
    }

    public static SKColor WithOpacity(this SKColor color, float opacity)
        => color.WithAlpha((byte)Math.Clamp(Math.Round(color.Alpha * Math.Clamp(opacity, 0f, 1f)), 0, 255));

    public static string ToHex(byte red, byte green, byte blue)
        => string.Create(CultureInfo.InvariantCulture, $"#{red:X2}{green:X2}{blue:X2}");
}
