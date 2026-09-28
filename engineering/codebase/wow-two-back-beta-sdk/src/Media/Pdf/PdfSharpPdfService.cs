using System.Globalization;
using Microsoft.Extensions.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;
using WoW.Two.Sdk.Backend.Beta.Media.Images;
using PigDocument = UglyToad.PdfPig.PdfDocument;
using PigParsingOptions = UglyToad.PdfPig.ParsingOptions;

namespace WoW.Two.Sdk.Backend.Beta.Media.Pdf;

/// <summary>
/// Provides <see cref="IPdfService"/> over PDFsharp (edit, write, protect, stamp) and PdfPig (information, text);
/// images reach PDF pages upright and re-encoded through <see cref="IImageService"/>.
/// </summary>
/// <param name="options">Limits and fonts; <c>Media:Pdf</c> reloads live.</param>
/// <param name="imageService">Normalizes images before they are embedded.</param>
public sealed class PdfSharpPdfService(IOptionsMonitor<PdfOptions> options, IImageService imageService) : IPdfService
{
    private const string FallbackFamily = "Arial";

    /// <inheritdoc />
    public async Task<PdfInfoResult> ReadInfoAsync(Stream pdf, string? password = null, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAsync(pdf, cancellationToken);
        using var document = OpenForReading(bytes, password);
        var information = document.Information;
        return new PdfInfoResult
        {
            PageCount = document.NumberOfPages,
            Pages = [.. Enumerable.Range(1, document.NumberOfPages).Select(number =>
            {
                var page = document.GetPage(number);
                return new PdfPageInfo { Number = number, Width = page.Width, Height = page.Height, Rotation = page.Rotation.Value };
            })],
            IsEncrypted = document.IsEncrypted,
            Version = document.Version.ToString("0.0", CultureInfo.InvariantCulture),
            Metadata = new PdfMetadataSpec
            {
                Title = information.Title,
                Author = information.Author,
                Subject = information.Subject,
                Keywords = information.Keywords,
                Creator = information.Creator,
                Producer = information.Producer,
            },
        };
    }

    /// <inheritdoc />
    public async Task<PdfResult> MergeAsync(IReadOnlyList<Stream> pdfs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfs);
        if (pdfs.Count == 0)
            throw new PdfRejectedException("pdf_empty", "Merging needs at least one PDF.");

        using var output = new PdfDocument();
        foreach (var pdf in pdfs)
        {
            using var source = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Import);
            foreach (var page in source.Pages)
                output.AddPage(page);
            GuardPages(output.PageCount);
        }

        return Save(output);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PdfResult>> SplitAsync(Stream pdf, int pagesPerPart, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pagesPerPart, 1);
        using var source = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Import);
        var parts = new List<PdfResult>();
        for (var first = 0; first < source.PageCount; first += pagesPerPart)
        {
            using var part = new PdfDocument();
            for (var index = first; index < Math.Min(first + pagesPerPart, source.PageCount); index++)
                part.AddPage(source.Pages[index]);
            parts.Add(Save(part));
        }

        return parts;
    }

    /// <inheritdoc />
    public async Task<PdfResult> ExtractPagesAsync(Stream pdf, IReadOnlyList<PdfPageRange> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        using var source = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();
        foreach (var number in Numbers(pages, source.PageCount))
            output.AddPage(source.Pages[number - 1]);

        if (output.PageCount == 0)
            throw new PdfRejectedException("pdf_empty", "The page list selects no pages.");
        GuardPages(output.PageCount);
        return Save(output);
    }

    /// <inheritdoc />
    public async Task<PdfResult> RemovePagesAsync(Stream pdf, IReadOnlyList<PdfPageRange> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);
        using var document = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Modify);
        var drop = Numbers(pages, document.PageCount).ToHashSet();
        if (drop.Count >= document.PageCount)
            throw new PdfRejectedException("pdf_empty", "Removing every page would leave an empty PDF.");

        foreach (var number in drop.OrderDescending())
            document.Pages.RemoveAt(number - 1);
        return Save(document);
    }

    /// <inheritdoc />
    public async Task<PdfResult> RotatePagesAsync(Stream pdf, int degrees, IReadOnlyList<PdfPageRange>? pages = null, CancellationToken cancellationToken = default)
    {
        if (degrees % 90 != 0)
            throw new PdfRejectedException("pdf_rotation_invalid", $"Pages turn in steps of 90 degrees, not {degrees}.");

        using var document = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Modify);
        foreach (var number in Selected(pages, document.PageCount))
        {
            var page = document.Pages[number - 1];
            page.Rotate = (((page.Rotate + degrees) % 360) + 360) % 360;
        }

        return Save(document);
    }

    /// <inheritdoc />
    public async Task<PdfResult> FromImagesAsync(IReadOnlyList<Stream> images, PdfImagesSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(spec);
        if (images.Count == 0)
            throw new PdfRejectedException("pdf_empty", "A PDF of images needs at least one image.");
        GuardPages(images.Count);

        using var document = new PdfDocument();
        foreach (var source in images)
        {
            var bytes = await ReadAsync(source, cancellationToken);
            var probe = await imageService.ProbeAsync(new MemoryStream(bytes, writable: false), cancellationToken);
            var picture = await imageService.EditAsync(
                new MemoryStream(bytes, writable: false),
                new ImageEditSpec
                {
                    Resize = new ImageResizeSpec { Width = spec.MaxImageDimension, Height = spec.MaxImageDimension },
                    Output = new ImageOutputSpec { Format = probe.HasAlpha ? ImageFormat.Png : ImageFormat.Jpeg, Quality = spec.ImageQuality },
                },
                cancellationToken);
            AddImagePage(document, picture, spec);
        }

        return Save(document);
    }

    /// <inheritdoc />
    public async Task<PdfResult> UpdateMetadataAsync(Stream pdf, PdfMetadataSpec metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        using var document = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Modify);
        var info = document.Info;
        if (metadata.Title is not null)
            info.Title = metadata.Title;
        if (metadata.Author is not null)
            info.Author = metadata.Author;
        if (metadata.Subject is not null)
            info.Subject = metadata.Subject;
        if (metadata.Keywords is not null)
            info.Keywords = metadata.Keywords;
        if (metadata.Creator is not null)
            info.Creator = metadata.Creator;
        return Save(document);
    }

    /// <inheritdoc />
    public async Task<PdfResult> EncryptAsync(Stream pdf, PdfEncryptSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrEmpty(spec.OwnerPassword);
        using var document = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Modify);
        var security = document.SecuritySettings;
        security.OwnerPassword = spec.OwnerPassword;
        if (!string.IsNullOrEmpty(spec.UserPassword))
            security.UserPassword = spec.UserPassword;
        security.PermitPrint = spec.AllowPrinting;
        security.PermitFullQualityPrint = spec.AllowPrinting;
        security.PermitExtractContent = spec.AllowCopying;
        security.PermitModifyDocument = spec.AllowEditing;
        security.PermitAssembleDocument = spec.AllowEditing;
        security.PermitAnnotations = spec.AllowAnnotations;
        security.PermitFormsFill = spec.AllowFormFilling;
        document.SecurityHandler.SetEncryptionToV5(true);
        return Save(document);
    }

    /// <inheritdoc />
    public async Task<PdfResult> DecryptAsync(Stream pdf, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var bytes = await ReadAsync(pdf, cancellationToken);
        try
        {
            using var document = Open(bytes, PdfDocumentOpenMode.Modify, password);
            document.SecurityHandler.SetEncryptionToNoneAndResetPasswords();
            return Save(document);
        }
        catch (Exception exception) when (exception is InvalidOperationException or PdfSharp.PdfSharpException)
        {
            using var readable = Open(bytes, PdfDocumentOpenMode.Import, password);
            using var copy = new PdfDocument();
            foreach (var page in readable.Pages)
                copy.AddPage(page);
            return Save(copy);
        }
    }

    /// <inheritdoc />
    public async Task<PdfTextResult> ExtractTextAsync(Stream pdf, IReadOnlyList<PdfPageRange>? pages = null, string? password = null, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadAsync(pdf, cancellationToken);
        using var document = OpenForReading(bytes, password);
        var text = new Dictionary<int, string>();
        foreach (var number in Selected(pages, document.NumberOfPages))
        {
            cancellationToken.ThrowIfCancellationRequested();
            text[number] = ContentOrderTextExtractor.GetText(document.GetPage(number));
        }

        return new PdfTextResult { Pages = text };
    }

    /// <inheritdoc />
    public async Task<PdfResult> AddWatermarkAsync(Stream pdf, PdfWatermarkSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(spec.Text);
        using var document = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Modify);
        var font = Font(spec.FontFamily, spec.FontSize, bold: true);
        var brush = new XSolidBrush(Color(spec.Color, spec.Opacity));
        foreach (var number in Selected(spec.Pages, document.PageCount))
        {
            var page = document.Pages[number - 1];
            using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var size = graphics.MeasureString(spec.Text, font);
            var state = graphics.Save();
            graphics.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
            graphics.RotateTransform(-spec.Angle);
            graphics.DrawString(spec.Text, font, brush, new XRect(-size.Width / 2, -size.Height / 2, size.Width, size.Height), XStringFormats.Center);
            graphics.Restore(state);
        }

        return Save(document);
    }

    /// <inheritdoc />
    public async Task<PdfResult> AddPageNumbersAsync(Stream pdf, PdfPageNumberSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        using var document = Open(await ReadAsync(pdf, cancellationToken), PdfDocumentOpenMode.Modify);
        var font = Font(spec.FontFamily, spec.FontSize, bold: false);
        var brush = new XSolidBrush(Color(spec.Color, 1));
        var selected = Selected(spec.Pages, document.PageCount).ToList();
        var total = spec.StartAt + selected.Count - 1;
        for (var index = 0; index < selected.Count; index++)
        {
            var page = document.Pages[selected[index] - 1];
            var label = spec.Format
                .Replace("{page}", (spec.StartAt + index).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{total}", total.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
            var size = graphics.MeasureString(label, font);
            var box = ImagePlacementMapper.Place((float)page.Width.Point, (float)page.Height.Point, (float)size.Width, (float)size.Height, spec.Anchor, (float)spec.Margin);
            graphics.DrawString(label, font, brush, new XRect(box.Left, box.Top, box.Width, box.Height), XStringFormats.Center);
        }

        return Save(document);
    }

    private static void AddImagePage(PdfDocument document, ImageResult picture, PdfImagesSpec spec)
    {
        using var image = XImage.FromStream(new MemoryStream(picture.Content, writable: false));
        var page = document.AddPage();
        double width, height, margin;
        if (spec.PageSize == PdfPageSize.Image)
        {
            (width, height, margin) = (picture.Width, picture.Height, 0);
        }
        else
        {
            (width, height) = spec.PageSize switch
            {
                PdfPageSize.Letter => (612d, 792d),
                PdfPageSize.Legal => (612d, 1008d),
                PdfPageSize.A3 => (841.89, 1190.55),
                PdfPageSize.A5 => (419.53, 595.28),
                _ => (595.28, 841.89),
            };
            if (spec.AutoOrient && picture.Width > picture.Height)
                (width, height) = (height, width);
            margin = Math.Max(0, spec.Margin);
        }

        page.Width = XUnit.FromPoint(width);
        page.Height = XUnit.FromPoint(height);
        var scale = Math.Min((width - (2 * margin)) / picture.Width, (height - (2 * margin)) / picture.Height);
        var (drawWidth, drawHeight) = (picture.Width * scale, picture.Height * scale);
        using var graphics = XGraphics.FromPdfPage(page);
        graphics.DrawImage(image, (width - drawWidth) / 2, (height - drawHeight) / 2, drawWidth, drawHeight);
    }

    private XFont Font(string? family, double size, bool bold)
    {
        PdfFontRepository.Install(options);
        return new XFont(
            family ?? options.CurrentValue.DefaultFontFamily ?? FallbackFamily,
            size,
            bold ? XFontStyleEx.Bold : XFontStyleEx.Regular,
            new XPdfFontOptions(PdfFontEncoding.Unicode));
    }

    private static XColor Color(string hex, double opacity)
    {
        var color = ImageColorMapper.Parse(hex);
        return XColor.FromArgb((int)Math.Round(Math.Clamp(opacity, 0, 1) * 255), color.Red, color.Green, color.Blue);
    }

    private async Task<byte[]> ReadAsync(Stream source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var limit = options.CurrentValue.MaxInputBytes;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
                throw new PdfRejectedException("pdf_too_large", $"The file is over the {limit}-byte input limit.");
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private PdfDocument Open(byte[] bytes, PdfDocumentOpenMode mode, string? password = null)
    {
        PdfDocument document;
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            document = password is null ? PdfReader.Open(stream, mode, null) : PdfReader.Open(stream, password, mode, null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            throw Rejected(exception, password);
        }

        GuardPages(document.PageCount);
        return document;
    }

    private PigDocument OpenForReading(byte[] bytes, string? password)
    {
        PigDocument document;
        try
        {
            document = PigDocument.Open(bytes, password is null ? null : new PigParsingOptions { Password = password });
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new PdfRejectedException(password is null ? "pdf_password_required" : "pdf_password_invalid", "The PDF is encrypted; supply its password.", exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            throw new PdfRejectedException("pdf_unreadable", "The file is not a PDF this library can read.", exception);
        }

        GuardPages(document.NumberOfPages);
        return document;
    }

    private static PdfRejectedException Rejected(Exception exception, string? password)
        => exception.Message.Contains("password", StringComparison.OrdinalIgnoreCase)
            ? new PdfRejectedException(password is null ? "pdf_password_required" : "pdf_password_invalid", "The PDF is encrypted; supply its password.", exception)
            : new PdfRejectedException("pdf_unreadable", "The file is not a PDF this library can read.", exception);

    private void GuardPages(int pages)
    {
        var limit = options.CurrentValue.MaxPages;
        if (pages > limit)
            throw new PdfRejectedException("pdf_too_large", $"The PDF has {pages} pages, over the {limit}-page limit.");
    }

    private static IEnumerable<int> Numbers(IReadOnlyList<PdfPageRange> pages, int pageCount)
        => pages.SelectMany(range => range.Pages(pageCount));

    private static IEnumerable<int> Selected(IReadOnlyList<PdfPageRange>? pages, int pageCount)
        => pages is null ? Enumerable.Range(1, pageCount) : Numbers(pages, pageCount).Distinct();

    /// <summary>Saves the document; PDFsharp locks it once saved, so the page count is read first.</summary>
    private static PdfResult Save(PdfDocument document)
    {
        var pages = document.PageCount;
        using var stream = new MemoryStream();
        document.Save(stream, false);
        return new PdfResult { Content = stream.ToArray(), PageCount = pages };
    }
}
