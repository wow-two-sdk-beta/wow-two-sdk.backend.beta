namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents a crop rectangle in pixels of the upright image; parts outside the image are clipped.</summary>
public sealed record ImageCropSpec
{
    /// <summary>Gets the left edge.</summary>
    public required int X { get; init; }

    /// <summary>Gets the top edge.</summary>
    public required int Y { get; init; }

    /// <summary>Gets the width.</summary>
    public required int Width { get; init; }

    /// <summary>Gets the height.</summary>
    public required int Height { get; init; }
}
