using Microsoft.Extensions.Options;
using SkiaSharp;

namespace WoW.Two.Sdk.Backend.Beta.Media.Images;

/// <summary>
/// Provides <see cref="IImageService"/> over SkiaSharp: decode within the configured limits, orient, crop, rotate,
/// resize, draw overlays, and encode JPEG, PNG or WebP — lowering quality, then size, to meet a byte budget.
/// </summary>
/// <param name="options">Limits, encoder defaults and fonts; <c>Media:Images</c> reloads live.</param>
public sealed class SkiaImageService(IOptionsMonitor<ImageOptions> options) : IImageService
{
    private readonly ImageTextRenderer _text = new(new SkiaFontRepository(options));

    /// <inheritdoc />
    public async Task<ImageProbeResult> ProbeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var data = await ReadAsync(source, cancellationToken);
        using var codec = OpenCodec(data);
        var (width, height) = ImageOrientationMapper.Upright(codec.Info.Width, codec.Info.Height, codec.EncodedOrigin);
        return new ImageProbeResult
        {
            Format = ImageFormatMapper.FromSkia(codec.EncodedFormat),
            Width = width,
            Height = height,
            Orientation = (int)codec.EncodedOrigin,
            HasAlpha = codec.Info.AlphaType != SKAlphaType.Opaque,
            FrameCount = Math.Max(1, codec.FrameCount),
        };
    }

    /// <inheritdoc />
    public async Task<ImageResult> EditAsync(Stream source, ImageEditSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        using var data = await ReadAsync(source, cancellationToken);
        var (bitmap, format) = Decode(data, spec.AutoOrient);
        try
        {
            var background = ImageColorMapper.Parse(spec.Output.Background);
            bitmap = Crop(bitmap, spec.Crop);
            bitmap = ImageOrientationMapper.Apply(bitmap, ImageOrientationMapper.FromRotation(spec.Rotate));
            if (spec.FlipHorizontal)
                bitmap = ImageOrientationMapper.Apply(bitmap, SKEncodedOrigin.TopRight);
            if (spec.FlipVertical)
                bitmap = ImageOrientationMapper.Apply(bitmap, SKEncodedOrigin.BottomLeft);
            bitmap = Resize(bitmap, spec.Resize, background);

            using (var canvas = new SKCanvas(bitmap))
            {
                if (spec.Watermark is { } watermark)
                    DrawWatermark(canvas, bitmap.Width, bitmap.Height, watermark);
                foreach (var text in spec.Texts)
                    _text.Render(canvas, bitmap.Width, bitmap.Height, text);
            }

            var output = spec.Output.Format ?? (ImageFormatMapper.IsWritable(format) ? format : ImageFormat.Png);
            return Encode(bitmap, output, spec.Output, cancellationToken);
        }
        finally
        {
            bitmap.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<ImageResult> CollageAsync(IReadOnlyList<Stream> sources, CollageSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(spec);
        var limits = options.CurrentValue;
        if (sources.Count is 0 or > 64)
            throw new ImageRejectedException("collage_count_invalid", $"A collage takes 1 to 64 images, not {sources.Count}.");
        if (spec.Width < 64 || spec.Width > limits.MaxOutputDimension)
            throw new ImageRejectedException("image_too_large", $"A collage must be 64 to {limits.MaxOutputDimension} pixels wide.");

        var bitmaps = new List<SKBitmap>(sources.Count);
        try
        {
            foreach (var source in sources)
            {
                using var data = await ReadAsync(source, cancellationToken);
                bitmaps.Add(Decode(data, autoOrient: true).Bitmap);
            }

            var count = bitmaps.Count;
            var columns = spec.Layout switch
            {
                CollageLayout.Row => count,
                CollageLayout.Column => 1,
                _ => Math.Clamp(spec.Columns ?? (int)Math.Ceiling(Math.Sqrt(count)), 1, count),
            };
            var rows = (int)Math.Ceiling(count / (double)columns);
            var aspect = spec.CellAspectRatio ?? (bitmaps[0].Width / (float)bitmaps[0].Height);
            var cellWidth = (spec.Width - (2f * spec.Padding) - ((columns - 1f) * spec.Gap)) / columns;
            if (cellWidth < 8 || aspect <= 0)
                throw new ImageRejectedException("collage_cells_too_small", "The collage leaves its cells under 8 pixels wide; widen it or reduce the gap.");

            var cellHeight = cellWidth / aspect;
            var height = (int)Math.Round((2f * spec.Padding) + (rows * cellHeight) + ((rows - 1f) * spec.Gap));
            if (height > limits.MaxOutputDimension)
                throw new ImageRejectedException("image_too_large", $"The collage would be {height} pixels tall, over the {limits.MaxOutputDimension} limit.");

            using var collage = new SKBitmap(new SKImageInfo(spec.Width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(collage))
            {
                canvas.Clear(ImageColorMapper.Parse(spec.Background));
                for (var index = 0; index < count; index++)
                {
                    var row = index / columns;
                    var inRow = row == rows - 1 ? count - (row * columns) : columns;
                    var offset = (columns - inRow) * (cellWidth + spec.Gap) / 2;
                    var cell = SKRect.Create(
                        spec.Padding + offset + (index % columns * (cellWidth + spec.Gap)),
                        spec.Padding + (row * (cellHeight + spec.Gap)),
                        cellWidth,
                        cellHeight);
                    canvas.Save();
                    if (spec.CornerRadius > 0)
                        canvas.ClipRoundRect(new SKRoundRect(cell, spec.CornerRadius, spec.CornerRadius), antialias: true);
                    DrawInto(canvas, bitmaps[index], cell, spec.Fit == ImageFit.Cover);
                    canvas.Restore();
                }

                foreach (var text in spec.Texts)
                    _text.Render(canvas, collage.Width, collage.Height, text);
            }

            return Encode(collage, spec.Output.Format ?? ImageFormat.Jpeg, spec.Output, cancellationToken);
        }
        finally
        {
            foreach (var bitmap in bitmaps)
                bitmap.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<ImageMetadataResult> ReadMetadataAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var data = await ReadAsync(source, cancellationToken);
        return ExifMetadataMapper.Map(data.AsSpan());
    }

    /// <inheritdoc />
    public async Task<ImageAnalysisResult> AnalyzeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        using var data = await ReadAsync(source, cancellationToken);
        using var bitmap = Decode(data, autoOrient: true).Bitmap;
        var scale = Math.Min(1f, 64f / Math.Max(bitmap.Width, bitmap.Height));
        var pixels = Opaque(bitmap, Math.Max(1, (int)Math.Round(bitmap.Width * scale)), Math.Max(1, (int)Math.Round(bitmap.Height * scale)), out var width, out var height);
        var (average, dominant) = ImagePaletteMapper.Map(pixels);
        var thumbnail = Opaque(bitmap, 9, 8, out _, out _);
        var gray = new byte[thumbnail.Length];
        for (var index = 0; index < thumbnail.Length; index++)
            gray[index] = (byte)((thumbnail[index].Red * 0.299) + (thumbnail[index].Green * 0.587) + (thumbnail[index].Blue * 0.114));

        return new ImageAnalysisResult
        {
            BlurHash = BlurHashMapper.Map(pixels, width, height),
            AverageColor = average,
            DominantColors = dominant,
            PerceptualHash = PerceptualHashMapper.Map(gray),
        };
    }

    private async Task<SKData> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var limit = options.CurrentValue.MaxInputBytes;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
                throw new ImageRejectedException("image_too_large", $"The image is over the {limit}-byte input limit.");
            buffer.Write(chunk, 0, read);
        }

        return SKData.CreateCopy(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    private static SKCodec OpenCodec(SKData data)
        => SKCodec.Create(data) ?? throw new ImageRejectedException("image_unreadable", "The data is not an image this platform can read.");

    private (SKBitmap Bitmap, ImageFormat Format) Decode(SKData data, bool autoOrient)
    {
        using var codec = OpenCodec(data);
        var info = codec.Info;
        var limit = options.CurrentValue.MaxPixels;
        if ((long)info.Width * info.Height > limit)
            throw new ImageRejectedException("image_too_large", $"The image is {info.Width}×{info.Height}, over the {limit}-pixel limit.");

        var target = new SKImageInfo(info.Width, info.Height, SKImageInfo.PlatformColorType, info.AlphaType == SKAlphaType.Opaque ? SKAlphaType.Opaque : SKAlphaType.Premul);
        var bitmap = new SKBitmap(target);
        var decoded = codec.GetPixels(target, bitmap.GetPixels());
        if (decoded is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            bitmap.Dispose();
            throw new ImageRejectedException("image_unreadable", $"The image could not be decoded ({decoded}).");
        }

        var format = ImageFormatMapper.FromSkia(codec.EncodedFormat);
        return (autoOrient ? ImageOrientationMapper.Apply(bitmap, codec.EncodedOrigin) : bitmap, format);
    }

    private static SKBitmap Crop(SKBitmap source, ImageCropSpec? crop)
    {
        if (crop is null)
            return source;

        var area = SKRectI.Intersect(new SKRectI(0, 0, source.Width, source.Height), SKRectI.Create(crop.X, crop.Y, crop.Width, crop.Height));
        if (area.Width <= 0 || area.Height <= 0)
            throw new ImageRejectedException("image_crop_empty", "The crop rectangle lies outside the image.");

        var target = new SKBitmap(new SKImageInfo(area.Width, area.Height, source.ColorType, source.AlphaType));
        using (var canvas = new SKCanvas(target))
            canvas.DrawBitmap(source, area, SKRect.Create(area.Width, area.Height));

        source.Dispose();
        return target;
    }

    private SKBitmap Resize(SKBitmap source, ImageResizeSpec? resize, SKColor background)
    {
        if (resize is null || (resize.Width is null && resize.Height is null))
            return source;

        var max = options.CurrentValue.MaxOutputDimension;
        if (resize.Width > max || resize.Height > max || resize.Width <= 0 || resize.Height <= 0)
            throw new ImageRejectedException("image_resize_invalid", $"A resize must stay between 1 and {max} pixels on each side.");

        var (width, height) = (source.Width, source.Height);
        var full = SKRect.Create(width, height);
        if (resize.Fit != ImageFit.Max && resize.Width is { } boxWidth && resize.Height is { } boxHeight)
        {
            var target = NewBitmap(boxWidth, boxHeight);
            using (var canvas = new SKCanvas(target))
            {
                switch (resize.Fit)
                {
                    case ImageFit.Cover:
                    {
                        var scale = Math.Max(boxWidth / (float)width, boxHeight / (float)height);
                        var area = SKRect.Create((width - (boxWidth / scale)) / 2, (height - (boxHeight / scale)) / 2, boxWidth / scale, boxHeight / scale);
                        DrawScaled(canvas, source, area, SKRect.Create(boxWidth, boxHeight));
                        break;
                    }

                    case ImageFit.Pad:
                    {
                        canvas.Clear(background);
                        var scale = Math.Min(boxWidth / (float)width, boxHeight / (float)height);
                        if (!resize.Upscale)
                            scale = Math.Min(scale, 1f);
                        var destination = SKRect.Create((boxWidth - (width * scale)) / 2, (boxHeight - (height * scale)) / 2, width * scale, height * scale);
                        DrawScaled(canvas, source, full, destination);
                        break;
                    }

                    default:
                        DrawScaled(canvas, source, full, SKRect.Create(boxWidth, boxHeight));
                        break;
                }
            }

            source.Dispose();
            return target;
        }

        var fit = Math.Min(resize.Width is { } w ? w / (float)width : float.MaxValue, resize.Height is { } h ? h / (float)height : float.MaxValue);
        if (!resize.Upscale)
            fit = Math.Min(fit, 1f);
        var (newWidth, newHeight) = (Math.Max(1, (int)Math.Round(width * fit)), Math.Max(1, (int)Math.Round(height * fit)));
        if (newWidth == width && newHeight == height)
            return source;

        var resized = Scale(source, newWidth, newHeight);
        source.Dispose();
        return resized;
    }

    private void DrawWatermark(SKCanvas canvas, int width, int height, ImageWatermarkSpec spec)
    {
        using var data = SKData.CreateCopy(spec.Content);
        using var mark = Decode(data, autoOrient: true).Bitmap;
        using var image = SKImage.FromBitmap(mark);
        using var paint = new SKPaint { Color = SKColors.White.WithOpacity(spec.Opacity), IsAntialias = true };
        var markWidth = Math.Max(1f, width * spec.RelativeWidth);
        var markHeight = markWidth * mark.Height / mark.Width;
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        if (!spec.Tile)
        {
            canvas.DrawImage(image, ImagePlacementMapper.Place(width, height, markWidth, markHeight, spec.Anchor, width * spec.Margin), sampling, paint);
            return;
        }

        var row = 0;
        for (var y = 0f; y < height; y += markHeight * 1.6f, row++)
        {
            for (var x = row % 2 * markWidth * 0.8f; x < width; x += markWidth * 1.6f)
                canvas.DrawImage(image, SKRect.Create(x, y, markWidth, markHeight), sampling, paint);
        }
    }

    /// <summary>Encodes, lowering the quality of a lossy format and then the size, until <see cref="ImageOutputSpec.MaxBytes"/> fits.</summary>
    private ImageResult Encode(SKBitmap bitmap, ImageFormat format, ImageOutputSpec output, CancellationToken cancellationToken)
    {
        var limits = options.CurrentValue;
        var encoder = ImageFormatMapper.ToSkia(format);
        var background = ImageColorMapper.Parse(output.Background);
        var quality = Math.Clamp(output.Quality ?? (format == ImageFormat.Webp ? limits.WebpQuality : limits.JpegQuality), 1, 100);
        var floor = Math.Clamp(Math.Min(limits.MinQuality, quality), 1, 100);
        var working = bitmap;
        try
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bytes = EncodeOnce(working, encoder, quality, background);
                if (output.MaxBytes is not { } budget || bytes.Length <= budget)
                    return new ImageResult { Content = bytes, Format = format, Width = working.Width, Height = working.Height };

                if (ImageFormatMapper.IsLossy(format))
                {
                    if (FitQuality(working, encoder, background, floor, quality, budget) is { } fitted)
                        return new ImageResult { Content = fitted, Format = format, Width = working.Width, Height = working.Height };
                    bytes = EncodeOnce(working, encoder, floor, background);
                }

                var ratio = Math.Min(0.9, Math.Sqrt(budget / (double)bytes.Length) * 0.95);
                var smaller = Scale(working, Math.Max(1, (int)(working.Width * ratio)), Math.Max(1, (int)(working.Height * ratio)));
                if (!ReferenceEquals(working, bitmap))
                    working.Dispose();
                working = smaller;
            }

            throw new ImageRejectedException("image_budget_unreachable", $"The image cannot be encoded within {output.MaxBytes} bytes.");
        }
        finally
        {
            if (!ReferenceEquals(working, bitmap))
                working.Dispose();
        }
    }

    /// <summary>The highest quality between <paramref name="low"/> and <paramref name="high"/> that fits <paramref name="budget"/>, or null.</summary>
    private static byte[]? FitQuality(SKBitmap bitmap, SKEncodedImageFormat encoder, SKColor background, int low, int high, long budget)
    {
        byte[]? best = null;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            var bytes = EncodeOnce(bitmap, encoder, middle, background);
            if (bytes.Length <= budget)
            {
                best = bytes;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return best;
    }

    private static byte[] EncodeOnce(SKBitmap bitmap, SKEncodedImageFormat encoder, int quality, SKColor background)
    {
        if (encoder == SKEncodedImageFormat.Jpeg && bitmap.AlphaType != SKAlphaType.Opaque)
        {
            using var flat = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKImageInfo.PlatformColorType, SKAlphaType.Opaque));
            using (var canvas = new SKCanvas(flat))
            {
                canvas.Clear(background.WithAlpha(255));
                canvas.DrawBitmap(bitmap, 0, 0);
            }

            return EncodeRaw(flat, encoder, quality);
        }

        return EncodeRaw(bitmap, encoder, quality);
    }

    private static byte[] EncodeRaw(SKBitmap bitmap, SKEncodedImageFormat encoder, int quality)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(encoder, quality)
            ?? throw new ImageRejectedException("image_encode_failed", $"The platform could not encode {encoder}.");
        return data.ToArray();
    }

    private static SKBitmap NewBitmap(int width, int height)
        => new(new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul));

    private static SKBitmap Scale(SKBitmap source, int width, int height)
    {
        var target = NewBitmap(width, height);
        using var canvas = new SKCanvas(target);
        DrawScaled(canvas, source, SKRect.Create(source.Width, source.Height), SKRect.Create(width, height));
        return target;
    }

    /// <summary>Draws <paramref name="source"/> into <paramref name="cell"/>: cropped to fill it, or letterboxed inside it.</summary>
    private static void DrawInto(SKCanvas canvas, SKBitmap source, SKRect cell, bool cover)
    {
        var (width, height) = (source.Width, source.Height);
        if (cover)
        {
            var scale = Math.Max(cell.Width / width, cell.Height / height);
            var area = SKRect.Create((width - (cell.Width / scale)) / 2, (height - (cell.Height / scale)) / 2, cell.Width / scale, cell.Height / scale);
            DrawScaled(canvas, source, area, cell);
            return;
        }

        var fit = Math.Min(cell.Width / width, cell.Height / height);
        var destination = SKRect.Create(cell.MidX - (width * fit / 2), cell.MidY - (height * fit / 2), width * fit, height * fit);
        DrawScaled(canvas, source, SKRect.Create(width, height), destination);
    }

    /// <summary>Draws with mipmaps when shrinking hard and a Mitchell cubic otherwise, so neither aliasing nor blur shows.</summary>
    private static void DrawScaled(SKCanvas canvas, SKBitmap source, SKRect area, SKRect destination)
    {
        using var image = SKImage.FromBitmap(source);
        var shrink = Math.Min(destination.Width / area.Width, destination.Height / area.Height);
        var sampling = shrink < 0.5f
            ? new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            : new SKSamplingOptions(SKCubicResampler.Mitchell);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(image, area, destination, sampling, paint);
    }

    /// <summary>The pixels of <paramref name="source"/> stretched to a size and flattened on white, as red, green, blue.</summary>
    private static (byte Red, byte Green, byte Blue)[] Opaque(SKBitmap source, int width, int height, out int resultWidth, out int resultHeight)
    {
        using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.White);
            DrawScaled(canvas, source, SKRect.Create(source.Width, source.Height), SKRect.Create(width, height));
        }

        var bytes = target.GetPixelSpan();
        var pixels = new (byte Red, byte Green, byte Blue)[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * target.RowBytes) + (x * 4);
                pixels[(y * width) + x] = (bytes[offset], bytes[offset + 1], bytes[offset + 2]);
            }
        }

        (resultWidth, resultHeight) = (width, height);
        return pixels;
    }
}
