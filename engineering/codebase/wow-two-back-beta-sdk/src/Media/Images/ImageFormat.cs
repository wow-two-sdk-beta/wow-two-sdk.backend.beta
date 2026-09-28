namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Refers to an image file format; JPEG, PNG and WebP can be written, the rest only read.</summary>
public enum ImageFormat
{
    /// <summary>JPEG — lossy, no transparency.</summary>
    Jpeg,

    /// <summary>PNG — lossless, transparency.</summary>
    Png,

    /// <summary>WebP — lossy, transparency.</summary>
    Webp,

    /// <summary>GIF — read only; the first frame.</summary>
    Gif,

    /// <summary>BMP — read only.</summary>
    Bmp,

    /// <summary>ICO — read only.</summary>
    Ico,

    /// <summary>HEIF/HEIC — read only where the platform decodes it.</summary>
    Heif,

    /// <summary>AVIF — read only where the platform decodes it.</summary>
    Avif,

    /// <summary>Any other format the platform decodes.</summary>
    Other,
}
