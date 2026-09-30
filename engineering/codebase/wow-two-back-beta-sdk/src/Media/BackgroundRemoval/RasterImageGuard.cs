using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.BackgroundRemoval;

/// <summary>Validated single-raster shape in upright EXIF coordinates.</summary>
public sealed record RasterImageInfo(int Width, int Height, string ContentType);

/// <summary>Checks encoded limits before decoding, then demands complete bounded raster content.</summary>
public static class RasterImageGuard
{
    /// <summary>Validates PNG, JPEG or WebP and returns its upright shape.</summary>
    public static RasterImageInfo Validate(byte[] bytes, int maximumBytes = 20 * 1024 * 1024,
        long maximumPixels = 20_000_000, int maximumDimension = 8192)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0 || bytes.Length > maximumBytes) throw Invalid();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp)) throw Invalid();
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > maximumDimension || info.Height > maximumDimension
            || (long)info.Width * info.Height > maximumPixels || codec.FrameCount > 1) throw Invalid();
        using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success) throw Invalid();
        if (codec.EncodedFormat == SKEncodedImageFormat.Png && !bytes.AsSpan().EndsWith(new byte[] {0,0,0,0,73,69,78,68,174,66,96,130})) throw Invalid();
        if (codec.EncodedFormat == SKEncodedImageFormat.Jpeg && (bytes.Length < 2 || bytes[^2] != 255 || bytes[^1] != 217)) throw Invalid();
        var swapped = (int)codec.EncodedOrigin is >= 5 and <= 8;
        var type = codec.EncodedFormat switch { SKEncodedImageFormat.Png => "image/png", SKEncodedImageFormat.Jpeg => "image/jpeg", _ => "image/webp" };
        return new(swapped ? info.Height : info.Width, swapped ? info.Width : info.Height, type);
    }
    private static BackgroundRemovalException Invalid() => new("invalid_image", "Use a complete single-frame PNG, JPEG or WebP within the image limits.");
}
