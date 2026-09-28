using System.IO.Packaging;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using Pictures = DocumentFormat.OpenXml.Drawing.Pictures;

namespace WoW.Two.Sdk.Backend.Beta.Media.Word;

/// <summary>Provides Word processing over the Open XML SDK: no Office install, no natives, streams in and bytes out.</summary>
/// <param name="options">Limits and typography; <c>Media:Word</c> reloads live.</param>
/// <param name="time">The clock built documents are stamped with.</param>
public sealed class OpenXmlWordService(IOptionsMonitor<WordOptions> options, TimeProvider time) : IWordService
{
    /// <inheritdoc />
    public async Task<WordInfoResult> ReadInfoAsync(Stream docx, CancellationToken cancellationToken = default)
    {
        using var buffer = await BufferAsync(docx, cancellationToken);
        return Read(buffer, editable: false, document =>
        {
            var body = Body(document);
            var properties = document.PackageProperties;
            var pages = document.ExtendedFilePropertiesPart?.Properties?.Pages?.Text;
            return new WordInfoResult
            {
                Title = Blank(properties.Title),
                Subject = Blank(properties.Subject),
                Author = Blank(properties.Creator),
                LastModifiedBy = Blank(properties.LastModifiedBy),
                Keywords = Blank(properties.Keywords),
                Description = Blank(properties.Description),
                Created = Utc(properties.Created),
                Modified = Utc(properties.Modified),
                ReportedPages = int.TryParse(pages, System.Globalization.CultureInfo.InvariantCulture, out var count) ? count : null,
                Words = WordTextMapper.CountWords(WordTextMapper.ToText(document.MainDocumentPart!, WordTextFormat.Plain)),
                Paragraphs = body.Descendants<Paragraph>().Count(),
                Tables = body.Descendants<Table>().Count(),
                Images = body.Descendants<Pictures.Picture>().Count(),
            };
        });
    }

    /// <inheritdoc />
    public async Task<WordTextResult> ExtractTextAsync(Stream docx, WordTextFormat format = WordTextFormat.Plain, CancellationToken cancellationToken = default)
    {
        using var buffer = await BufferAsync(docx, cancellationToken);
        return Read(buffer, editable: false, document =>
        {
            Body(document);
            var text = WordTextMapper.ToText(document.MainDocumentPart!, format);
            var words = format == WordTextFormat.Plain ? text : WordTextMapper.ToText(document.MainDocumentPart!, WordTextFormat.Plain);
            return new WordTextResult { Text = text, Format = format, Words = WordTextMapper.CountWords(words) };
        });
    }

    /// <inheritdoc />
    public async Task<WordResult> FillTemplateAsync(Stream template, WordTemplateSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Data.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new WordRejectedException("word_template_data_invalid", "Template data must be a JSON object.");

        using var buffer = await BufferAsync(template, cancellationToken);
        Read(buffer, editable: true, document =>
        {
            Body(document);
            var misses = WordTemplateMapper.Fill(document.MainDocumentPart!, spec.Data, spec.MissingValues);
            if (spec.MissingValues == WordMissingValue.Reject && misses.Count > 0)
            {
                throw new WordRejectedException(
                    "word_template_value_missing",
                    $"The template data lacks: {string.Join(", ", misses.Take(10))}{(misses.Count > 10 ? ", …" : string.Empty)}.");
            }

            return true;
        });
        return new WordResult { Content = buffer.ToArray() };
    }

    /// <inheritdoc />
    public Task<WordResult> FromMarkdownAsync(string markdown, WordDocumentSpec? spec = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var current = options.CurrentValue;
        if (Encoding.UTF8.GetByteCount(markdown) > current.MaxInputBytes)
            throw new WordRejectedException("word_too_large", $"The Markdown is over the {current.MaxInputBytes}-byte limit.");

        spec ??= new WordDocumentSpec();
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body());
            main.AddNewPart<StyleDefinitionsPart>().Styles = WordStylesMapper.Styles(spec.FontFamily ?? current.FontFamily, spec.FontSizePoints ?? current.FontSizePoints, current.CodeFontFamily);
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = WordStylesMapper.Numbering();
            var body = main.Document.Body!;
            new MarkdownWordRenderer(main).Render(markdown, body);
            body.Append(Section(spec));

            var now = time.GetUtcNow().UtcDateTime;
            document.PackageProperties.Title = spec.Title;
            document.PackageProperties.Creator = spec.Author;
            document.PackageProperties.Created = now;
            document.PackageProperties.Modified = now;
        }

        return Task.FromResult(new WordResult { Content = stream.ToArray() });
    }

    private static SectionProperties Section(WordDocumentSpec spec)
    {
        var (width, height) = spec.Paper == WordPaperSize.Letter ? (12240u, 15840u) : (11906u, 16838u);
        var margin = (int)Math.Round(Math.Clamp(spec.MarginMillimeters, 0, 100) * 1440 / 25.4);
        return new SectionProperties(
            new PageSize { Width = width, Height = height },
            new PageMargin { Top = margin, Bottom = margin, Left = (uint)margin, Right = (uint)margin, Header = 720, Footer = 720, Gutter = 0 });
    }

    /// <summary>Copies the input into memory under the size limit.</summary>
    private async Task<MemoryStream> BufferAsync(Stream input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var limit = options.CurrentValue.MaxInputBytes;
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                await buffer.DisposeAsync();
                throw new WordRejectedException("word_too_large", $"The document is over the {limit}-byte limit.");
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <summary>Opens the buffered package and runs <paramref name="work"/>; anything that is not a Word document is refused.</summary>
    private T Read<T>(MemoryStream buffer, bool editable, Func<WordprocessingDocument, T> work)
    {
        try
        {
            using var document = WordprocessingDocument.Open(buffer, editable, new OpenSettings
            {
                AutoSave = editable,
                MaxCharactersInPart = options.CurrentValue.MaxCharactersPerPart,
            });
            return work(document);
        }
        catch (Exception exception) when (exception is OpenXmlPackageException or FileFormatException or InvalidDataException or XmlException or InvalidOperationException)
        {
            throw new WordRejectedException("word_unreadable", "The file is not a readable Word document.", exception);
        }
    }

    private static Body Body(WordprocessingDocument document)
        => document.MainDocumentPart?.Document?.Body ?? throw new WordRejectedException("word_unreadable", "The file has no Word document body.");

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateTimeOffset? Utc(DateTime? value)
        => value is { } stamp ? new DateTimeOffset(DateTime.SpecifyKind(stamp, stamp.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : stamp.Kind).ToUniversalTime()) : null;
}
