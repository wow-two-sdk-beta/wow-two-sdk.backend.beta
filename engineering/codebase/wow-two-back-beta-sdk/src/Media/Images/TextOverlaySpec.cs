namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents text drawn on an image: a caption, a label or a tiled watermark.</summary>
public sealed record TextOverlaySpec
{
    /// <summary>Gets the text; line breaks start new lines and long lines wrap at <see cref="MaxWidth"/>.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the font family; null takes the configured default, then the platform default.</summary>
    public string? FontFamily { get; init; }

    /// <summary>Gets the font size in pixels; null derives it from <see cref="RelativeFontSize"/>.</summary>
    public float? FontSize { get; init; }

    /// <summary>Gets the font size as a share of the image width. Default 0.05.</summary>
    public float RelativeFontSize { get; init; } = 0.05f;

    /// <summary>Gets whether the text is bold.</summary>
    public bool Bold { get; init; }

    /// <summary>Gets whether the text is italic.</summary>
    public bool Italic { get; init; }

    /// <summary>Gets the text color as <c>#RRGGBB[AA]</c>. Default white.</summary>
    public string Color { get; init; } = "#FFFFFF";

    /// <summary>Gets the opacity, 0–1, applied on top of the color's own alpha. Default 1.</summary>
    public float Opacity { get; init; } = 1f;

    /// <summary>Gets where the text block sits. Default <see cref="ImageAnchor.BottomRight"/>.</summary>
    public ImageAnchor Anchor { get; init; } = ImageAnchor.BottomRight;

    /// <summary>Gets the distance from the anchored edges as a share of the image width. Default 0.03.</summary>
    public float Margin { get; init; } = 0.03f;

    /// <summary>Gets the widest a line may run as a share of the image width. Default 0.9.</summary>
    public float MaxWidth { get; init; } = 0.9f;

    /// <summary>Gets a box color drawn behind the text as <c>#RRGGBB[AA]</c>; null draws none.</summary>
    public string? Background { get; init; }

    /// <summary>Gets whether a soft shadow lifts the text off busy backgrounds. Default false.</summary>
    public bool Shadow { get; init; }

    /// <summary>Gets the clockwise rotation in degrees around the text block's center.</summary>
    public float Rotation { get; init; }

    /// <summary>Gets whether the text repeats across the whole image as a watermark, ignoring the anchor.</summary>
    public bool Tile { get; init; }
}
