namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents how the result is encoded.</summary>
public sealed record ImageOutputSpec
{
    /// <summary>Gets the format; null keeps the source format when writable, else PNG. Only JPEG, PNG and WebP are writable.</summary>
    public ImageFormat? Format { get; init; }

    /// <summary>Gets the JPEG/WebP quality, 1–100; null takes the configured default.</summary>
    public int? Quality { get; init; }

    /// <summary>
    /// Gets the byte budget: lossy formats lower their quality, then every format shrinks, until the result fits.
    /// Null leaves the size as encoded.
    /// </summary>
    public long? MaxBytes { get; init; }

    /// <summary>Gets the color transparent areas become in JPEG, and padding everywhere, as <c>#RRGGBB[AA]</c>. Default white.</summary>
    public string Background { get; init; } = "#FFFFFF";
}
