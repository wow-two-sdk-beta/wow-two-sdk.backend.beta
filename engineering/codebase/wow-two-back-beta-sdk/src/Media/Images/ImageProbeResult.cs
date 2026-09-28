namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents what an image is, read from its header without decoding the pixels.</summary>
public sealed record ImageProbeResult
{
    /// <summary>Gets the format.</summary>
    public required ImageFormat Format { get; init; }

    /// <summary>Gets the width as displayed, after the EXIF orientation.</summary>
    public required int Width { get; init; }

    /// <summary>Gets the height as displayed, after the EXIF orientation.</summary>
    public required int Height { get; init; }

    /// <summary>Gets the EXIF orientation, 1–8; 1 is upright.</summary>
    public required int Orientation { get; init; }

    /// <summary>Gets whether any pixel can be transparent.</summary>
    public required bool HasAlpha { get; init; }

    /// <summary>Gets the frames; above 1 for an animation, of which only the first is edited.</summary>
    public required int FrameCount { get; init; }

    /// <summary>Gets the media type, such as <c>image/jpeg</c>.</summary>
    public string ContentType => ImageFormatMapper.ToContentType(Format);
}
