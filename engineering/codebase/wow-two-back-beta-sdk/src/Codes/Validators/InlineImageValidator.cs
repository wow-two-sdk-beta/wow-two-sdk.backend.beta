using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Validators;

/// <summary>Validates inline raster image data used by code logos.</summary>
internal sealed class InlineImageValidator
{
    internal bool IsValid(string? value) => TryGetPixelCount(value, out _);

    internal bool TryGetPixelCount(string? value, out long pixels)
    {
        pixels = 0;
        if (string.IsNullOrEmpty(value) || value.Length > 1_400_000) return false;
        var png = value.StartsWith("data:image/png;base64,", StringComparison.Ordinal);
        var jpeg = value.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal);
        if (!png && !jpeg) return false;
        try
        {
            var bytes = Convert.FromBase64String(value[(value.IndexOf(',') + 1)..]);
            if (bytes.Length > 1_048_576) return false;
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            if (codec is null) return false;
            pixels = (long)codec.Info.Width * codec.Info.Height;
            return codec is not null
                && codec.EncodedFormat == (png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg)
                && codec.Info.Width is > 0 and <= 4096 && codec.Info.Height is > 0 and <= 4096
                && (long)codec.Info.Width * codec.Info.Height <= 16_000_000;
        }
        catch (FormatException) { return false; }
    }
}
