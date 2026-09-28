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
    public async Task Forms_ShouldReadFillAndLockFields()
    {
        var form = await Pdf.ReadFormAsync(Stream(FormPdf()));

        form.Select(field => (field.Name, field.Type, field.Value)).Should().Equal(
            ("name", PdfFormFieldType.Text, "old"),
            ("agree", PdfFormFieldType.CheckBox, "Off"),
            ("city", PdfFormFieldType.ComboBox, "Tashkent"));
        form[1].Options.Should().Equal("Yes");
        form[2].Options.Should().Equal("Tashkent", "Samarkand", "Bukhara");

        var filled = await Pdf.FillFormAsync(Stream(FormPdf()), new PdfFormFillSpec
        {
            Values = new Dictionary<string, string> { ["name"] = "Алишер Навоий", ["agree"] = "yes", ["city"] = "Samarkand" },
            LockFields = true,
        });

        var read = await Pdf.ReadFormAsync(Stream(filled.Content));
        read.Select(field => field.Value).Should().Equal("Алишер Навоий", "Yes", "Samarkand");
        read.Should().OnlyContain(field => field.ReadOnly);

        (await Reject(() => Pdf.FillFormAsync(Stream(FormPdf()), new PdfFormFillSpec { Values = new Dictionary<string, string> { ["surname"] = "x" } }))).Should().Be("pdf_form_field_unknown");
        (await Reject(() => Pdf.FillFormAsync(Stream(FormPdf()), new PdfFormFillSpec { Values = new Dictionary<string, string> { ["city"] = "Paris" } }))).Should().Be("pdf_form_value_invalid");
        var plain = await Pdf.FromImagesAsync([Png(10, 10)], new PdfImagesSpec());
        (await Pdf.ReadFormAsync(Stream(plain.Content))).Should().BeEmpty();
        (await Reject(() => Pdf.FillFormAsync(Stream(plain.Content), new PdfFormFillSpec { Values = new Dictionary<string, string>() }))).Should().Be("pdf_form_missing");
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

    /// <summary>A one-page PDF with a text box, a check box and a combo box, written object by object with a true cross-reference table.</summary>
    private static byte[] FormPdf()
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 5 0 R 6 0 R] /DA (/Helv 0 Tf 0 g) /DR << /Font << /Helv 7 0 R >> >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 5 0 R 6 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (old) /Rect [50 700 300 720] /P 3 0 R /F 4 /DA (/Helv 12 Tf 0 g) >>",
            "<< /Type /Annot /Subtype /Widget /FT /Btn /T (agree) /V /Off /AS /Off /Rect [50 650 70 670] /P 3 0 R /F 4 /AP << /N << /Yes 8 0 R /Off 9 0 R >> >> >>",
            "<< /Type /Annot /Subtype /Widget /FT /Ch /Ff 131072 /T (city) /Opt [(Tashkent) (Samarkand) (Bukhara)] /V (Tashkent) /Rect [50 600 200 620] /P 3 0 R /F 4 /DA (/Helv 12 Tf 0 g) >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /XObject /Subtype /Form /BBox [0 0 20 20] /Length 0 >>\nstream\n\nendstream",
            "<< /Type /XObject /Subtype /Form /BBox [0 0 20 20] /Length 0 >>\nstream\n\nendstream",
        ];
        var pdf = new System.Text.StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        pdf.Append(System.Globalization.CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(pdf.ToString());
    }

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
