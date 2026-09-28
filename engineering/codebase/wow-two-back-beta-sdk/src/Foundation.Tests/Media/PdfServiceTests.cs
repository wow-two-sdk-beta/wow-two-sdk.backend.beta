using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Media.Pdf;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Media;

/// <summary>PDF tools over PDFsharp and PdfPig: image pages, page surgery, metadata, AES-256, stamps read back as text.</summary>
public sealed class PdfServiceTests
{
    private static readonly IPdfService Pdf = Build();

    [Fact]
    public async Task FromImages_ShouldPlaceOneUprightImagePerPage_TurningWidePagesLandscape()
    {
        var pdf = await Pdf.FromImagesAsync([Png(300, 400), Png(400, 300)], new PdfImagesSpec());

        var info = await Pdf.ReadInfoAsync(Stream(pdf.Content));

        info.PageCount.Should().Be(2);
        (Math.Round(info.Pages[0].Width), Math.Round(info.Pages[0].Height)).Should().Be((595, 842));
        (Math.Round(info.Pages[1].Width), Math.Round(info.Pages[1].Height)).Should().Be((842, 595));
        info.IsEncrypted.Should().BeFalse();
    }

    [Fact]
    public async Task Pages_ShouldMergeSplitExtractRemoveAndRotate()
    {
        var first = await Pdf.FromImagesAsync([Png(100, 200), Png(300, 100)], new PdfImagesSpec { PageSize = PdfPageSize.Image });
        var second = await Pdf.FromImagesAsync([Png(150, 150)], new PdfImagesSpec { PageSize = PdfPageSize.Image });

        var merged = await Pdf.MergeAsync([Stream(first.Content), Stream(second.Content)]);
        merged.PageCount.Should().Be(3);
        (await Sizes(merged)).Should().Equal((100, 200), (300, 100), (150, 150));

        (await Pdf.SplitAsync(Stream(merged.Content), 2)).Select(part => part.PageCount).Should().Equal(2, 1);
        (await Sizes(await Pdf.ExtractPagesAsync(Stream(merged.Content), PdfPageRange.Parse("3,1")))).Should().Equal((150, 150), (100, 200));
        (await Sizes(await Pdf.RemovePagesAsync(Stream(merged.Content), PdfPageRange.Parse("2")))).Should().Equal((100, 200), (150, 150));

        var rotated = await Pdf.RotatePagesAsync(Stream(merged.Content), 90, PdfPageRange.Parse("1"));
        (await Pdf.ReadInfoAsync(Stream(rotated.Content))).Pages.Select(page => page.Rotation).Should().Equal(90, 0, 0);
    }

    [Fact]
    public async Task Metadata_ShouldBeWrittenAndReadBack()
    {
        var pdf = await Pdf.FromImagesAsync([Png(100, 100)], new PdfImagesSpec());

        var updated = await Pdf.UpdateMetadataAsync(Stream(pdf.Content), new PdfMetadataSpec { Title = "Quarterly report", Author = "Acme" });

        var metadata = (await Pdf.ReadInfoAsync(Stream(updated.Content))).Metadata;
        (metadata.Title, metadata.Author).Should().Be(("Quarterly report", "Acme"));
    }

    [Fact]
    public async Task Encryption_ShouldLockWithAes_AndUnlockKeepingThePages()
    {
        var pdf = await Pdf.FromImagesAsync([Png(100, 100), Png(100, 100)], new PdfImagesSpec());

        var locked = await Pdf.EncryptAsync(Stream(pdf.Content), new PdfEncryptSpec { UserPassword = "open", OwnerPassword = "owner" });

        (await Reject(() => Pdf.ReadInfoAsync(Stream(locked.Content)))).Should().Be("pdf_password_required");
        (await Reject(() => Pdf.ReadInfoAsync(Stream(locked.Content), "wrong"))).Should().Be("pdf_password_invalid");
        (await Reject(() => Pdf.MergeAsync([Stream(locked.Content)]))).Should().Be("pdf_password_required");
        var opened = await Pdf.ReadInfoAsync(Stream(locked.Content), "open");
        (opened.IsEncrypted, opened.PageCount).Should().Be((true, 2));

        var unlocked = await Pdf.DecryptAsync(Stream(locked.Content), "owner");
        var info = await Pdf.ReadInfoAsync(Stream(unlocked.Content));
        (info.IsEncrypted, info.PageCount).Should().Be((false, 2));
    }

    [Fact]
    public async Task Stamps_ShouldWriteTextThatReadsBack_InAnyScript()
    {
        var pdf = await Pdf.FromImagesAsync([Png(100, 100), Png(100, 100), Png(100, 100)], new PdfImagesSpec());

        var numbered = await Pdf.AddPageNumbersAsync(Stream(pdf.Content), new PdfPageNumberSpec { Format = "Page {page} of {total}" });
        var stamped = await Pdf.AddWatermarkAsync(Stream(numbered.Content), new PdfWatermarkSpec { Text = "ЧЕРНОВИК DRAFT", Pages = PdfPageRange.Parse("1") });

        var text = await Pdf.ExtractTextAsync(Stream(stamped.Content));
        text.Pages[2].Should().Contain("Page 2 of 3");
        text.Pages[1].Should().Contain("DRAFT").And.Contain("ЧЕРНОВИК");
        text.Pages[3].Should().NotContain("DRAFT");
        (await Pdf.ExtractTextAsync(Stream(stamped.Content), PdfPageRange.Parse("3"))).Pages.Keys.Should().Equal(3);
    }

    [Fact]
    public async Task Compress_ShouldShrinkPhotos_KeepingPagesAndText()
    {
        var pdf = await Pdf.FromImagesAsync([Photo(1600, 1200)], new PdfImagesSpec { ImageQuality = 95, MaxImageDimension = 3000 });
        var numbered = await Pdf.AddPageNumbersAsync(Stream(pdf.Content), new PdfPageNumberSpec());

        var compressed = await Pdf.CompressAsync(Stream(numbered.Content), new PdfCompressSpec { ImageQuality = 50, MaxImageDimension = 800 });

        compressed.Content.Length.Should().BeLessThan(numbered.Content.Length / 3);
        compressed.PageCount.Should().Be(1);
        (await Pdf.ExtractTextAsync(Stream(compressed.Content))).Pages[1].Should().Contain("1 / 1");
        using var document = UglyToad.PdfPig.PdfDocument.Open(compressed.Content);
        var image = document.GetPage(1).GetImages().Single();
        (image.WidthInSamples, image.HeightInSamples).Should().Be((800, 600));
        using var decoded = SKBitmap.Decode(image.RawMemory.ToArray());
        decoded.Width.Should().Be(800, "the embedded stream stays a valid JPEG");
    }

    [Fact]
    public async Task Refusals_ShouldCarryStableReasons()
    {
        var pdf = await Pdf.FromImagesAsync([Png(100, 100), Png(100, 100), Png(100, 100)], new PdfImagesSpec());

        (await Reject(() => Pdf.ReadInfoAsync(Stream("not a pdf"u8.ToArray())))).Should().Be("pdf_unreadable");
        (await Reject(() => Pdf.MergeAsync([Stream("not a pdf"u8.ToArray())]))).Should().Be("pdf_unreadable");
        (await Reject(() => Pdf.ExtractPagesAsync(Stream(pdf.Content), PdfPageRange.Parse("2-5")))).Should().Be("pdf_page_range_invalid");
        (await Reject(() => Pdf.RemovePagesAsync(Stream(pdf.Content), PdfPageRange.Parse("1-")))).Should().Be("pdf_empty");
        (await Reject(() => Build(o => o.MaxPages = 2).SplitAsync(Stream(pdf.Content), 1))).Should().Be("pdf_too_large");
        FluentActions.Invoking(() => PdfPageRange.Parse("1-x")).Should().Throw<PdfRejectedException>();
        PdfPageRange.Parse("1-3, 5, 8-").Should().BeEquivalentTo([new PdfPageRange { From = 1, To = 3 }, PdfPageRange.Single(5), new PdfPageRange { From = 8 }]);
    }

    [Fact]
    public void Rejections_ShouldMapToValidationErrors()
    {
        using var provider = new ServiceCollection().AddPdfProcessing().BuildServiceProvider();

        var error = provider.GetServices<IExceptionMappingRule>().Select(rule => rule.TryMap(new PdfRejectedException("pdf_password_required", "Locked."))).First(mapped => mapped is not null)!;

        error.Type.Should().Be(AppErrorType.Validation);
        error.Metadata!["messageKey"].Should().Be("pdf_password_required");
    }

    private static IPdfService Build(Action<PdfOptions>? configure = null)
        => new ServiceCollection().AddPdfProcessing(configure).BuildServiceProvider().GetRequiredService<IPdfService>();

    private static async Task<IReadOnlyList<(double Width, double Height)>> Sizes(PdfResult pdf)
        => [.. (await Pdf.ReadInfoAsync(Stream(pdf.Content))).Pages.Select(page => (Math.Round(page.Width), Math.Round(page.Height)))];

    private static async Task<string> Reject(Func<Task> act)
        => (await act.Should().ThrowAsync<PdfRejectedException>()).Which.Reason;

    private static MemoryStream Stream(byte[] bytes) => new(bytes);

    /// <summary>A noisy photo, which JPEG cannot squeeze much at high quality.</summary>
    private static MemoryStream Photo(int width, int height)
    {
        var random = new Random(3);
        var pixels = new SKColor[width * height];
        for (var index = 0; index < pixels.Length; index++)
            pixels[index] = new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        using var bitmap = new SKBitmap(width, height) { Pixels = pixels };
        using var image = SKImage.FromBitmap(bitmap);
        return new MemoryStream(image.Encode(SKEncodedImageFormat.Jpeg, 95).ToArray());
    }

    private static MemoryStream Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
            canvas.Clear(SKColors.SteelBlue);
        using var image = SKImage.FromBitmap(bitmap);
        return new MemoryStream(image.Encode(SKEncodedImageFormat.Png, 100).ToArray());
    }
}
