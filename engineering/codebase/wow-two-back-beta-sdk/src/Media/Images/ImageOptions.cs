namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Holds the limits, encoder defaults and fonts that steer image processing.</summary>
/// <remarks>Set in code with <c>AddImageProcessing(o => …)</c> or in the host section <c>Media:Images</c>, which is applied last.</remarks>
public sealed record ImageOptions
{
    /// <summary>The host configuration section.</summary>
    public const string SectionName = "Media:Images";

    /// <summary>Gets or sets the largest encoded input accepted, in bytes. Default 50 MB.</summary>
    public long MaxInputBytes { get; set; } = 50L * 1024 * 1024;

    /// <summary>Gets or sets the most pixels an input may decode to, guarding against decompression bombs. Default 50 megapixels.</summary>
    public long MaxPixels { get; set; } = 50_000_000;

    /// <summary>Gets or sets the widest or tallest a result may be, in pixels. Default 8192.</summary>
    public int MaxOutputDimension { get; set; } = 8192;

    /// <summary>Gets or sets the JPEG quality when a spec names none. Default 85.</summary>
    public int JpegQuality { get; set; } = 85;

    /// <summary>Gets or sets the WebP quality when a spec names none. Default 80.</summary>
    public int WebpQuality { get; set; } = 80;

    /// <summary>Gets or sets the lowest quality a byte budget may push a lossy format to before shrinking it. Default 40.</summary>
    public int MinQuality { get; set; } = 40;

    /// <summary>Gets or sets the font family texts use when they name none; null takes the platform default.</summary>
    public string? DefaultFontFamily { get; set; }

    /// <summary>Gets font files by family name, such as <c>Inter</c> → <c>/app/fonts/Inter.ttf</c>; they win over system fonts.</summary>
    public Dictionary<string, string> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);
}
