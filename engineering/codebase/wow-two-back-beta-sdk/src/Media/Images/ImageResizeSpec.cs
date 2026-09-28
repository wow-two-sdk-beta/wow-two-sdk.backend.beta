namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents a target size and how the image meets it; one side alone keeps the aspect ratio.</summary>
public sealed record ImageResizeSpec
{
    /// <summary>Gets the target width in pixels; null derives it from the height.</summary>
    public int? Width { get; init; }

    /// <summary>Gets the target height in pixels; null derives it from the width.</summary>
    public int? Height { get; init; }

    /// <summary>Gets how the image meets the box. Default <see cref="ImageFit.Max"/>.</summary>
    public ImageFit Fit { get; init; } = ImageFit.Max;

    /// <summary>Gets whether a smaller image may grow; false keeps small images as they are. Default false.</summary>
    public bool Upscale { get; init; }
}
