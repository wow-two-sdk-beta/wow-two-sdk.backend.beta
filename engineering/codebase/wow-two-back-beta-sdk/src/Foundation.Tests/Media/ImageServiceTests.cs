using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Images;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media;

/// <summary>Image editing over SkiaSharp: orientation, fits, byte budgets, overlays, collages, analysis and limits.</summary>
public sealed class ImageServiceTests
{
    private static readonly IImageService Images = Build();

    [Fact]
    public async Task Probe_ShouldReportTheUprightSizeAndTheExifOrientation()
    {
        var probe = await Images.ProbeAsync(Stream(WithOrientation(Halves(200, 100, SKEncodedImageFormat.Jpeg), 6)));

        probe.Should().BeEquivalentTo(new { Format = ImageFormat.Jpeg, Width = 100, Height = 200, Orientation = 6, HasAlpha = false, FrameCount = 1 });
        probe.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Edit_ShouldStandTheImageUpright_AndDropTheExifOrientation()
    {
        var result = await Images.EditAsync(Stream(WithOrientation(Halves(200, 100, SKEncodedImageFormat.Jpeg), 6)), new ImageEditSpec());

        (result.Width, result.Height, result.Format).Should().Be((100, 200, ImageFormat.Jpeg));
        using var upright = Decode(result.Content);
        IsNear(upright.GetPixel(50, 40), SKColors.Red).Should().BeTrue("the stored left half turns to the top");
        IsNear(upright.GetPixel(50, 160), SKColors.Blue).Should().BeTrue();
        (await Images.ProbeAsync(Stream(result.Content))).Orientation.Should().Be(1);
    }

    [Fact]
    public async Task Edit_ShouldCropRotateAndFlip_InOrder()
    {
        var spec = new ImageEditSpec
        {
            Crop = new ImageCropSpec { X = 0, Y = 0, Width = 150, Height = 100 },
            Rotate = 90,
            FlipVertical = true,
            Output = new ImageOutputSpec { Format = ImageFormat.Png },
        };

        var result = await Images.EditAsync(Stream(Halves(200, 100, SKEncodedImageFormat.Png)), spec);

        (result.Width, result.Height).Should().Be((100, 150));
        using var bitmap = Decode(result.Content);
        IsNear(bitmap.GetPixel(50, 140), SKColors.Red).Should().BeTrue("rotating puts red on top, flipping moves it down");
        IsNear(bitmap.GetPixel(50, 10), SKColors.Blue).Should().BeTrue();
    }

    [Theory]
    [InlineData(ImageFit.Max, 100, 100, false, 100, 50)]
    [InlineData(ImageFit.Cover, 100, 100, false, 100, 100)]
    [InlineData(ImageFit.Pad, 100, 100, false, 100, 100)]
    [InlineData(ImageFit.Stretch, 100, 100, false, 100, 100)]
    [InlineData(ImageFit.Max, 800, null, false, 400, 200)]
    [InlineData(ImageFit.Max, 800, null, true, 800, 400)]
    [InlineData(ImageFit.Cover, null, 50, false, 100, 50)]
    public async Task Resize_ShouldMeetTheBoxAsTheFitSays(ImageFit fit, int? width, int? height, bool upscale, int expectedWidth, int expectedHeight)
    {
        var spec = new ImageEditSpec { Resize = new ImageResizeSpec { Width = width, Height = height, Fit = fit, Upscale = upscale } };

        var result = await Images.EditAsync(Stream(Halves(400, 200, SKEncodedImageFormat.Png)), spec);

        (result.Width, result.Height).Should().Be((expectedWidth, expectedHeight));
    }

    [Fact]
    public async Task Pad_ShouldFillTheRestWithTheBackground()
    {
        var spec = new ImageEditSpec
        {
            Resize = new ImageResizeSpec { Width = 100, Height = 100, Fit = ImageFit.Pad },
            Output = new ImageOutputSpec { Format = ImageFormat.Png, Background = "#00FF00" },
        };

        using var bitmap = Decode((await Images.EditAsync(Stream(Halves(400, 200, SKEncodedImageFormat.Png)), spec)).Content);

        IsNear(bitmap.GetPixel(50, 5), SKColors.Lime).Should().BeTrue();
        IsNear(bitmap.GetPixel(10, 50), SKColors.Red).Should().BeTrue();
    }

    [Theory]
    [InlineData(ImageFormat.Jpeg, 40_000)]
    [InlineData(ImageFormat.Webp, 30_000)]
    [InlineData(ImageFormat.Png, 60_000)]
    public async Task Output_ShouldMeetTheByteBudget(ImageFormat format, long budget)
    {
        var spec = new ImageEditSpec { Output = new ImageOutputSpec { Format = format, MaxBytes = budget } };

        var result = await Images.EditAsync(Stream(Noise(800, 600)), spec);

        result.Content.Length.Should().BeLessThanOrEqualTo((int)budget);
        result.Format.Should().Be(format);
        Decode(result.Content).Width.Should().Be(result.Width);
    }

    [Fact]
    public async Task Text_ShouldDrawAtItsAnchor_InAnyScript()
    {
        var spec = new ImageEditSpec
        {
            Texts =
            [
                new TextOverlaySpec { Text = "Hello, world", Anchor = ImageAnchor.TopLeft, Background = "#FF0000CC", FontSize = 24 },
                new TextOverlaySpec { Text = "Привет, Oʻzbekiston", Anchor = ImageAnchor.Bottom, Shadow = true, FontSize = 20 },
            ],
            Output = new ImageOutputSpec { Format = ImageFormat.Png },
        };

        using var bitmap = Decode((await Images.EditAsync(Stream(Solid(400, 200, SKColors.Black)), spec)).Content);

        Changed(bitmap, SKRectI.Create(0, 0, 200, 60), SKColors.Black).Should().BeTrue();
        Changed(bitmap, SKRectI.Create(60, 150, 280, 50), SKColors.Black).Should().BeTrue();
        Changed(bitmap, SKRectI.Create(300, 40, 100, 80), SKColors.Black).Should().BeFalse("nothing is anchored on the right middle");
    }

    [Fact]
    public async Task TiledText_ShouldCoverEveryQuadrant()
    {
        var spec = new ImageEditSpec
        {
            Texts = [new TextOverlaySpec { Text = "SAMPLE", Tile = true, Opacity = 0.5f, FontSize = 18 }],
            Output = new ImageOutputSpec { Format = ImageFormat.Png },
        };

        using var bitmap = Decode((await Images.EditAsync(Stream(Solid(400, 400, SKColors.Black)), spec)).Content);

        foreach (var quadrant in new[] { SKRectI.Create(0, 0, 200, 200), SKRectI.Create(200, 0, 200, 200), SKRectI.Create(0, 200, 200, 200), SKRectI.Create(200, 200, 200, 200) })
            Changed(bitmap, quadrant, SKColors.Black).Should().BeTrue();
    }

    [Fact]
    public async Task ImageWatermark_ShouldBlendAtItsAnchor()
    {
        var spec = new ImageEditSpec
        {
            Watermark = new ImageWatermarkSpec { Content = Solid(50, 50, SKColors.Red), RelativeWidth = 0.25f, Opacity = 1f },
            Output = new ImageOutputSpec { Format = ImageFormat.Png },
        };

        using var bitmap = Decode((await Images.EditAsync(Stream(Solid(400, 400, SKColors.White)), spec)).Content);

        IsNear(bitmap.GetPixel(330, 330), SKColors.Red).Should().BeTrue();
        IsNear(bitmap.GetPixel(40, 40), SKColors.White).Should().BeTrue();
    }

    [Fact]
    public async Task Collage_Duo_ShouldPlaceTheImagesSideBySide()
    {
        var spec = new CollageSpec { Layout = CollageLayout.Row, Width = 1000, Gap = 20, Padding = 10, Output = new ImageOutputSpec { Format = ImageFormat.Png } };

        var result = await Images.CollageAsync([Stream(Solid(300, 400, SKColors.Red)), Stream(Solid(300, 400, SKColors.Blue))], spec);

        (result.Width, result.Height).Should().Be((1000, 660));
        using var bitmap = Decode(result.Content);
        IsNear(bitmap.GetPixel(250, 330), SKColors.Red).Should().BeTrue();
        IsNear(bitmap.GetPixel(750, 330), SKColors.Blue).Should().BeTrue();
        IsNear(bitmap.GetPixel(500, 330), SKColors.White).Should().BeTrue("the gap shows the background");
    }

    [Fact]
    public async Task Collage_Grid_ShouldCenterAnIncompleteLastRow()
    {
        var spec = new CollageSpec { Width = 420, Gap = 20, Padding = 0, CellAspectRatio = 1, CornerRadius = 12, Background = "#000000", Output = new ImageOutputSpec { Format = ImageFormat.Png } };

        var result = await Images.CollageAsync([Stream(Solid(10, 10, SKColors.Red)), Stream(Solid(10, 10, SKColors.Lime)), Stream(Solid(10, 10, SKColors.Blue))], spec);

        (result.Width, result.Height).Should().Be((420, 420));
        using var bitmap = Decode(result.Content);
        IsNear(bitmap.GetPixel(210, 320), SKColors.Blue).Should().BeTrue("the third cell sits centered on the second row");
        IsNear(bitmap.GetPixel(40, 320), SKColors.Black).Should().BeTrue();
        IsNear(bitmap.GetPixel(1, 1), SKColors.Black).Should().BeTrue("rounded corners leave the background showing");
    }

    [Fact]
    public async Task Analyze_ShouldSummarizeTheImage()
    {
        var red = await Images.AnalyzeAsync(Stream(Solid(120, 80, SKColors.Red)));

        red.BlurHash.Should().HaveLength(28).And.StartWith("L", "4×3 components encode as size flag 21");
        red.BlurHash[2..6].Should().Be("TI:j", "the DC term decodes back to exactly #FF0000");
        red.AverageColor.Should().Be("#FF0000");
        red.DominantColors[0].Should().BeEquivalentTo(new ImageColorShare { Color = "#FF0000", Share = 1f });

        var gradient = await Images.AnalyzeAsync(Stream(Gradient(200, 150, 0)));
        var brighter = await Images.AnalyzeAsync(Stream(Gradient(200, 150, 12)));
        var mirrored = await Images.AnalyzeAsync(Stream(Gradient(200, 150, 0, mirrored: true)));
        gradient.PerceptualHash.IsNearDuplicateOf(brighter.PerceptualHash).Should().BeTrue();
        gradient.PerceptualHash.Distance(mirrored.PerceptualHash).Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task Limits_ShouldRejectOversizedUnreadableAndUnwritableImages()
    {
        var strict = Build(o => o.MaxPixels = 1_000);

        (await FluentActions.Awaiting(() => strict.EditAsync(Stream(Solid(100, 100, SKColors.Red)), new ImageEditSpec())).Should().ThrowAsync<ImageRejectedException>())
            .Which.Reason.Should().Be("image_too_large");
        (await FluentActions.Awaiting(() => Images.ProbeAsync(Stream("not an image"u8.ToArray()))).Should().ThrowAsync<ImageRejectedException>())
            .Which.Reason.Should().Be("image_unreadable");
        (await FluentActions.Awaiting(() => Images.EditAsync(Stream(Solid(10, 10, SKColors.Red)), new ImageEditSpec { Output = new ImageOutputSpec { Format = ImageFormat.Gif } })).Should().ThrowAsync<ImageRejectedException>())
            .Which.Reason.Should().Be("image_format_unwritable");
    }

    [Fact]
    public void Rejections_ShouldMapToValidationErrors()
    {
        using var provider = new ServiceCollection().AddImageProcessing().BuildServiceProvider();

        var error = provider.GetServices<IExceptionMappingRule>().Select(rule => rule.TryMap(new ImageRejectedException("image_too_large", "Too big."))).First(mapped => mapped is not null)!;

        error.Type.Should().Be(AppErrorType.Validation);
        error.Metadata!["messageKey"].Should().Be("image_too_large");
    }

    private static IImageService Build(Action<ImageOptions>? configure = null)
        => new ServiceCollection().AddImageProcessing(configure).BuildServiceProvider().GetRequiredService<IImageService>();

    private static MemoryStream Stream(byte[] bytes) => new(bytes);

    private static SKBitmap Decode(byte[] bytes) => SKBitmap.Decode(bytes);

    private static bool IsNear(SKColor actual, SKColor expected, int tolerance = 40)
        => Math.Abs(actual.Red - expected.Red) <= tolerance && Math.Abs(actual.Green - expected.Green) <= tolerance && Math.Abs(actual.Blue - expected.Blue) <= tolerance;

    private static bool Changed(SKBitmap bitmap, SKRectI area, SKColor background)
    {
        for (var y = area.Top; y < area.Bottom; y++)
        {
            for (var x = area.Left; x < area.Right; x++)
            {
                if (!IsNear(bitmap.GetPixel(x, y), background, 10))
                    return true;
            }
        }

        return false;
    }

    private static byte[] Solid(int width, int height, SKColor color)
        => Paint(width, height, SKEncodedImageFormat.Png, canvas => canvas.Clear(color));

    private static byte[] Halves(int width, int height, SKEncodedImageFormat format)
        => Paint(width, height, format, canvas =>
        {
            using var red = new SKPaint { Color = SKColors.Red };
            using var blue = new SKPaint { Color = SKColors.Blue };
            canvas.DrawRect(SKRect.Create(0, 0, width / 2f, height), red);
            canvas.DrawRect(SKRect.Create(width / 2f, 0, width / 2f, height), blue);
        });

    private static byte[] Gradient(int width, int height, byte lift, bool mirrored = false)
        => Paint(width, height, SKEncodedImageFormat.Png, canvas =>
        {
            for (var x = 0; x < width; x++)
            {
                var level = (byte)Math.Min(255, (mirrored ? width - x : x) * 200 / width + lift);
                using var paint = new SKPaint { Color = new SKColor(level, level, level) };
                canvas.DrawRect(SKRect.Create(x, 0, 1, height), paint);
            }
        });

    private static byte[] Noise(int width, int height)
    {
        var random = new Random(7);
        using var bitmap = new SKBitmap(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
        }

        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static byte[] Paint(int width, int height, SKEncodedImageFormat format, Action<SKCanvas> draw)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
            draw(canvas);
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(format, 95).ToArray();
    }

    /// <summary>Inserts an EXIF APP1 segment holding only the orientation tag right after the JPEG start marker.</summary>
    private static byte[] WithOrientation(byte[] jpeg, byte orientation)
    {
        byte[] app1 =
        [
            0xFF, 0xE1, 0x00, 0x22, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08,
            0x00, 0x01,
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }
}
