using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Places a box of a given size against an anchor of a canvas, inset by a margin.</summary>
internal static class ImagePlacementMapper
{
    public static SKRect Place(float width, float height, float boxWidth, float boxHeight, ImageAnchor anchor, float margin)
    {
        var x = Horizontal(anchor) switch
        {
            < 0 => margin,
            > 0 => width - margin - boxWidth,
            _ => (width - boxWidth) / 2,
        };
        var y = Vertical(anchor) switch
        {
            < 0 => margin,
            > 0 => height - margin - boxHeight,
            _ => (height - boxHeight) / 2,
        };
        return SKRect.Create(x, y, boxWidth, boxHeight);
    }

    /// <summary>-1 for the left column, 0 for the middle, 1 for the right.</summary>
    public static int Horizontal(ImageAnchor anchor) => anchor switch
    {
        ImageAnchor.TopLeft or ImageAnchor.Left or ImageAnchor.BottomLeft => -1,
        ImageAnchor.TopRight or ImageAnchor.Right or ImageAnchor.BottomRight => 1,
        _ => 0,
    };

    /// <summary>-1 for the top row, 0 for the middle, 1 for the bottom.</summary>
    public static int Vertical(ImageAnchor anchor) => anchor switch
    {
        ImageAnchor.TopLeft or ImageAnchor.Top or ImageAnchor.TopRight => -1,
        ImageAnchor.BottomLeft or ImageAnchor.Bottom or ImageAnchor.BottomRight => 1,
        _ => 0,
    };
}
