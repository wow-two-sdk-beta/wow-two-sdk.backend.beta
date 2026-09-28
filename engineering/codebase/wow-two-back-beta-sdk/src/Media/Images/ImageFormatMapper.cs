using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Maps image formats to media types, extensions and Skia's encoder formats.</summary>
public static class ImageFormatMapper
{
    /// <summary>The media type of <paramref name="format"/>.</summary>
    /// <param name="format">The format.</param>
    public static string ToContentType(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => "image/jpeg",
        ImageFormat.Png => "image/png",
        ImageFormat.Webp => "image/webp",
        ImageFormat.Gif => "image/gif",
        ImageFormat.Bmp => "image/bmp",
        ImageFormat.Ico => "image/x-icon",
        ImageFormat.Heif => "image/heif",
        ImageFormat.Avif => "image/avif",
        _ => "application/octet-stream",
    };

    /// <summary>The usual file extension of <paramref name="format"/>, without the dot.</summary>
    /// <param name="format">The format.</param>
    public static string ToExtension(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => "jpg",
        ImageFormat.Png => "png",
        ImageFormat.Webp => "webp",
        ImageFormat.Gif => "gif",
        ImageFormat.Bmp => "bmp",
        ImageFormat.Ico => "ico",
        ImageFormat.Heif => "heic",
        ImageFormat.Avif => "avif",
        _ => "bin",
    };

    /// <summary>Whether the SDK can write <paramref name="format"/>.</summary>
    /// <param name="format">The format.</param>
    public static bool IsWritable(ImageFormat format) => format is ImageFormat.Jpeg or ImageFormat.Png or ImageFormat.Webp;

    /// <summary>Whether <paramref name="format"/> drops detail to save bytes.</summary>
    /// <param name="format">The format.</param>
    public static bool IsLossy(ImageFormat format) => format is ImageFormat.Jpeg or ImageFormat.Webp;

    internal static ImageFormat FromSkia(SKEncodedImageFormat format) => format switch
    {
        SKEncodedImageFormat.Jpeg => ImageFormat.Jpeg,
        SKEncodedImageFormat.Png => ImageFormat.Png,
        SKEncodedImageFormat.Webp => ImageFormat.Webp,
        SKEncodedImageFormat.Gif => ImageFormat.Gif,
        SKEncodedImageFormat.Bmp => ImageFormat.Bmp,
        SKEncodedImageFormat.Ico => ImageFormat.Ico,
        SKEncodedImageFormat.Heif => ImageFormat.Heif,
        SKEncodedImageFormat.Avif => ImageFormat.Avif,
        _ => ImageFormat.Other,
    };

    internal static SKEncodedImageFormat ToSkia(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => SKEncodedImageFormat.Jpeg,
        ImageFormat.Png => SKEncodedImageFormat.Png,
        ImageFormat.Webp => SKEncodedImageFormat.Webp,
        _ => throw new ImageRejectedException("image_format_unwritable", $"Images cannot be written as {format}; use JPEG, PNG or WebP."),
    };
}
