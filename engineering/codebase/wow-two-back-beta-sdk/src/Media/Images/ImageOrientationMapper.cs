using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Maps an EXIF orientation, a rotation or a flip to a new upright bitmap; the source is disposed when replaced.</summary>
internal static class ImageOrientationMapper
{
    public static SKBitmap Apply(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
            return source;

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (width, height) = (source.Width, source.Height);
        var target = new SKBitmap(new SKImageInfo(swap ? height : width, swap ? width : height, source.ColorType, source.AlphaType));
        using (var canvas = new SKCanvas(target))
        {
            switch (origin)
            {
                case SKEncodedOrigin.TopRight:
                    canvas.Translate(width, 0);
                    canvas.Scale(-1, 1);
                    break;
                case SKEncodedOrigin.BottomRight:
                    canvas.Translate(width, height);
                    canvas.RotateDegrees(180);
                    break;
                case SKEncodedOrigin.BottomLeft:
                    canvas.Translate(0, height);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.LeftTop:
                    canvas.RotateDegrees(90);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.RightTop:
                    canvas.Translate(height, 0);
                    canvas.RotateDegrees(90);
                    break;
                case SKEncodedOrigin.RightBottom:
                    canvas.Translate(height, width);
                    canvas.RotateDegrees(-90);
                    canvas.Scale(1, -1);
                    break;
                case SKEncodedOrigin.LeftBottom:
                    canvas.Translate(0, width);
                    canvas.RotateDegrees(-90);
                    break;
            }

            canvas.DrawBitmap(source, 0, 0);
        }

        source.Dispose();
        return target;
    }

    /// <summary>The upright size of an image stored with <paramref name="origin"/>.</summary>
    public static (int Width, int Height) Upright(int width, int height, SKEncodedOrigin origin)
        => origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom
            ? (height, width)
            : (width, height);

    /// <summary>The orientation that turns an upright image clockwise by <paramref name="degrees"/>.</summary>
    public static SKEncodedOrigin FromRotation(int degrees) => (((degrees % 360) + 360) % 360) switch
    {
        0 => SKEncodedOrigin.TopLeft,
        90 => SKEncodedOrigin.RightTop,
        180 => SKEncodedOrigin.BottomRight,
        270 => SKEncodedOrigin.LeftBottom,
        _ => throw new ImageRejectedException("image_rotation_invalid", $"Rotation must be 0, 90, 180 or 270 degrees, not {degrees}."),
    };
}
