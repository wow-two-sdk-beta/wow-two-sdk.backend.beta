namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents an image (a logo) drawn on top of another, placed once or tiled.</summary>
public sealed record ImageWatermarkSpec
{
    /// <summary>Gets the encoded watermark image; PNG keeps its transparency.</summary>
    public required byte[] Content { get; init; }

    /// <summary>Gets the watermark width as a share of the image width. Default 0.2.</summary>
    public float RelativeWidth { get; init; } = 0.2f;

    /// <summary>Gets the opacity, 0–1. Default 0.6.</summary>
    public float Opacity { get; init; } = 0.6f;

    /// <summary>Gets where the watermark sits. Default <see cref="ImageAnchor.BottomRight"/>.</summary>
    public ImageAnchor Anchor { get; init; } = ImageAnchor.BottomRight;

    /// <summary>Gets the distance from the anchored edges as a share of the image width. Default 0.03.</summary>
    public float Margin { get; init; } = 0.03f;

    /// <summary>Gets whether the watermark repeats across the whole image, ignoring the anchor.</summary>
    public bool Tile { get; init; }
}
