using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>
/// Maps a page's JPEG image objects to smaller ones in place: decoded, downscaled to a size limit and re-encoded at a
/// quality — kept only when the result is smaller. Form objects are followed; shared images are visited once.
/// </summary>
internal static class PdfImageRecompressionMapper
{
    public static void Recompress(PdfDictionary? resources, PdfCompressSpec spec, HashSet<PdfDictionary> seen, int depth = 0)
    {
        if (depth > 8 || resources?.Elements.GetDictionary("/XObject") is not { } objects)
            return;

        foreach (var item in objects.Elements.Values)
        {
            if ((item as PdfReference)?.Value is not PdfDictionary target || !seen.Add(target))
                continue;

            switch (target.Elements.GetName("/Subtype"))
            {
                case "/Form":
                    Recompress(target.Elements.GetDictionary("/Resources"), spec, seen, depth + 1);
                    break;
                case "/Image":
                    RecompressJpeg(target, spec);
                    break;
            }
        }
    }

    private static void RecompressJpeg(PdfDictionary image, PdfCompressSpec spec)
    {
        if (image.Stream is null || !IsJpegOnly(image) || image.Elements.ContainsKey("/Mask") || image.Elements.GetBoolean("/ImageMask"))
            return;

        var gray = ColorSpaceOf(image) switch
        {
            "/DeviceGray" => true,
            "/DeviceRGB" or "/DeviceCMYK" => false,
            _ => (bool?)null,
        };
        if (gray is null)
            return;

        var original = image.Stream.Value;
        using var codec = SKCodec.Create(new MemoryStream(original, writable: false));
        if (codec is null)
            return;

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, gray.Value ? SKColorType.Gray8 : SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var decoded = new SKBitmap(info);
        if (codec.GetPixels(info, decoded.GetPixels()) is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            return;

        var scale = Math.Min(1f, spec.MaxImageDimension / (float)Math.Max(info.Width, info.Height));
        var (width, height) = (Math.Max(1, (int)(info.Width * scale)), Math.Max(1, (int)(info.Height * scale)));
        using var resized = scale < 1f ? decoded.Resize(info.WithSize(width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)) : null;
        using var picture = SKImage.FromBitmap(resized ?? decoded);
        using var encoded = picture.Encode(SKEncodedImageFormat.Jpeg, spec.ImageQuality);
        if (encoded is null || encoded.Size >= original.Length)
            return;

        image.Stream.Value = encoded.ToArray();
        image.Elements.SetInteger("/Width", width);
        image.Elements.SetInteger("/Height", height);
        image.Elements.SetInteger("/BitsPerComponent", 8);
        image.Elements.SetName("/Filter", "/DCTDecode");
        image.Elements.SetName("/ColorSpace", gray.Value ? "/DeviceGray" : "/DeviceRGB");
        image.Elements.Remove("/DecodeParms");
        image.Elements.Remove("/Decode");
    }

    /// <summary>Whether the image's only filter is <c>/DCTDecode</c>, so its stream is a JPEG file.</summary>
    private static bool IsJpegOnly(PdfDictionary image) => image.Elements.GetValue("/Filter") switch
    {
        PdfName name => name.Value == "/DCTDecode",
        PdfArray { Elements.Count: 1 } filters => (filters.Elements[0] as PdfName)?.Value == "/DCTDecode",
        _ => false,
    };

    /// <summary>The device color space, reading an ICC profile's component count when the space is ICC-based.</summary>
    private static string? ColorSpaceOf(PdfDictionary image) => image.Elements.GetValue("/ColorSpace") switch
    {
        PdfName name => name.Value,
        PdfArray { Elements.Count: 2 } array when (array.Elements[0] as PdfName)?.Value == "/ICCBased"
            => ((array.Elements[1] as PdfReference)?.Value as PdfDictionary)?.Elements.GetInteger("/N") switch
            {
                1 => "/DeviceGray",
                3 => "/DeviceRGB",
                4 => "/DeviceCMYK",
                _ => null,
            },
        _ => null,
    };
}
