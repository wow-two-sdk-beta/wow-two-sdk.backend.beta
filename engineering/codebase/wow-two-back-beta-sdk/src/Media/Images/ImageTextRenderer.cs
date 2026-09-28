using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>Renders <see cref="TextOverlaySpec"/>s onto a canvas: wrapped, anchored, boxed, shadowed, rotated or tiled.</summary>
/// <param name="fonts">Finds the typeface each text needs.</param>
internal sealed class ImageTextRenderer(SkiaFontRepository fonts)
{
    public void Render(SKCanvas canvas, int width, int height, TextOverlaySpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (string.IsNullOrWhiteSpace(spec.Text))
            return;

        var size = spec.FontSize ?? Math.Max(8f, width * spec.RelativeFontSize);
        using var font = new SKFont(fonts.Find(spec.FontFamily, spec.Bold, spec.Italic, spec.Text), size)
        {
            Subpixel = true,
            Edging = SKFontEdging.Antialias,
        };
        using var paint = new SKPaint { Color = ImageColorMapper.Parse(spec.Color).WithOpacity(spec.Opacity), IsAntialias = true };
        font.GetFontMetrics(out var metrics);
        var lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
        var lines = Wrap(spec.Text, font, Math.Max(size, width * spec.MaxWidth));
        var block = new SKSize(lines.Max(line => font.MeasureText(line)), lineHeight * lines.Count);

        if (spec.Tile)
        {
            RenderTiled(canvas, width, height, spec, font, paint, lines, block, lineHeight, metrics.Ascent);
            return;
        }

        var padding = spec.Background is null ? 0f : size * 0.4f;
        var box = ImagePlacementMapper.Place(width, height, block.Width + (2 * padding), block.Height + (2 * padding), spec.Anchor, width * spec.Margin);
        canvas.Save();
        if (spec.Rotation != 0)
            canvas.RotateDegrees(spec.Rotation, box.MidX, box.MidY);

        if (spec.Background is { } background)
        {
            using var boxPaint = new SKPaint { Color = ImageColorMapper.Parse(background).WithOpacity(spec.Opacity), IsAntialias = true };
            canvas.DrawRoundRect(box, padding * 0.6f, padding * 0.6f, boxPaint);
        }

        for (var index = 0; index < lines.Count; index++)
        {
            var lineWidth = font.MeasureText(lines[index]);
            var x = ImagePlacementMapper.Horizontal(spec.Anchor) switch
            {
                < 0 => box.Left + padding,
                > 0 => box.Right - padding - lineWidth,
                _ => box.MidX - (lineWidth / 2),
            };
            var baseline = box.Top + padding - metrics.Ascent + (index * lineHeight);
            DrawLine(canvas, lines[index], x, baseline, font, paint, spec.Shadow);
        }

        canvas.Restore();
    }

    private static void RenderTiled(
        SKCanvas canvas,
        int width,
        int height,
        TextOverlaySpec spec,
        SKFont font,
        SKPaint paint,
        List<string> lines,
        SKSize block,
        float lineHeight,
        float ascent)
    {
        var stepX = block.Width + (font.Size * 3);
        var stepY = block.Height + (font.Size * 3);
        var reach = MathF.Sqrt((width * width) + (height * height));
        canvas.Save();
        canvas.RotateDegrees(spec.Rotation == 0 ? -30 : spec.Rotation, width / 2f, height / 2f);
        var row = 0;
        for (var y = -reach; y < height + reach; y += stepY, row++)
        {
            for (var x = -reach + (row % 2 * stepX / 2); x < width + reach; x += stepX)
            {
                for (var index = 0; index < lines.Count; index++)
                    DrawLine(canvas, lines[index], x, y - ascent + (index * lineHeight), font, paint, spec.Shadow);
            }
        }

        canvas.Restore();
    }

    private static void DrawLine(SKCanvas canvas, string line, float x, float baseline, SKFont font, SKPaint paint, bool shadow)
    {
        if (shadow)
        {
            using var shade = new SKPaint
            {
                Color = SKColors.Black.WithAlpha((byte)(paint.Color.Alpha * 0.55f)),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, font.Size * 0.06f),
            };
            var offset = font.Size * 0.05f;
            canvas.DrawText(line, x + offset, baseline + offset, SKTextAlign.Left, font, shade);
        }

        canvas.DrawText(line, x, baseline, SKTextAlign.Left, font, paint);
    }

    /// <summary>Splits on line breaks, then wraps each line at word boundaries so none runs past <paramref name="maxWidth"/>.</summary>
    private static List<string> Wrap(string text, SKFont font, float maxWidth)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var current = string.Empty;
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (current.Length > 0 && font.MeasureText(candidate) > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = candidate;
                }
            }

            lines.Add(current);
        }

        return lines;
    }
}
