namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Represents a collage: how the images are arranged, spaced and framed, and how the result is encoded.</summary>
public sealed record CollageSpec
{
    /// <summary>Gets the arrangement. Default <see cref="CollageLayout.Grid"/>.</summary>
    public CollageLayout Layout { get; init; } = CollageLayout.Grid;

    /// <summary>Gets the columns of a grid; null takes the smallest square that holds every image.</summary>
    public int? Columns { get; init; }

    /// <summary>Gets the width of the result in pixels. Default 2048.</summary>
    public int Width { get; init; } = 2048;

    /// <summary>Gets each cell's width divided by its height; null takes the first image's aspect ratio.</summary>
    public float? CellAspectRatio { get; init; }

    /// <summary>Gets how an image meets its cell: <see cref="ImageFit.Cover"/> crops, <see cref="ImageFit.Max"/> letterboxes.</summary>
    public ImageFit Fit { get; init; } = ImageFit.Cover;

    /// <summary>Gets the space between cells in pixels. Default 16.</summary>
    public int Gap { get; init; } = 16;

    /// <summary>Gets the frame around the cells in pixels. Default 16.</summary>
    public int Padding { get; init; } = 16;

    /// <summary>Gets the corner radius of each cell in pixels. Default 0.</summary>
    public float CornerRadius { get; init; }

    /// <summary>Gets the background as <c>#RRGGBB[AA]</c>. Default white.</summary>
    public string Background { get; init; } = "#FFFFFF";

    /// <summary>Gets texts drawn over the finished collage, such as a caption.</summary>
    public IReadOnlyList<TextOverlaySpec> Texts { get; init; } = [];

    /// <summary>Gets how the result is encoded; the default is JPEG.</summary>
    public ImageOutputSpec Output { get; init; } = new() { Format = ImageFormat.Jpeg };
}
