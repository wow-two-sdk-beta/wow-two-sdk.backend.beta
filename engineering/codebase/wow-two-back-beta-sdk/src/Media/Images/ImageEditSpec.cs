namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>
/// Represents one edit applied in a single pass, in this order: orient, crop, rotate and flip, resize, watermark,
/// texts, encode. An empty spec re-encodes the image upright and without metadata.
/// </summary>
public sealed record ImageEditSpec
{
    /// <summary>Gets whether the EXIF orientation is applied, so the result stands upright. Default true.</summary>
    public bool AutoOrient { get; init; } = true;

    /// <summary>Gets the crop rectangle in pixels of the upright image.</summary>
    public ImageCropSpec? Crop { get; init; }

    /// <summary>Gets the clockwise rotation: 0, 90, 180 or 270 degrees.</summary>
    public int Rotate { get; init; }

    /// <summary>Gets whether the image is mirrored left to right.</summary>
    public bool FlipHorizontal { get; init; }

    /// <summary>Gets whether the image is mirrored top to bottom.</summary>
    public bool FlipVertical { get; init; }

    /// <summary>Gets the target size.</summary>
    public ImageResizeSpec? Resize { get; init; }

    /// <summary>Gets an image drawn on top, such as a logo.</summary>
    public ImageWatermarkSpec? Watermark { get; init; }

    /// <summary>Gets the texts drawn on top, in order.</summary>
    public IReadOnlyList<TextOverlaySpec> Texts { get; init; } = [];

    /// <summary>Gets how the result is encoded.</summary>
    public ImageOutputSpec Output { get; init; } = new();
}
