# Media

*Media & document processing — images, PDF, Word, Markdown, caption parsing, YouTube links and tabular (CSV/Excel) export. Own domain logic + thin wraps over permissive libs.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Media`.

Exporter types live under `Tabular/Exporters/`, `Csv/Exporters/` and `Excel/Exporters/`, with matching namespaces.
Parser types live under `Captions/Parsers/` and `Csv/Parsers/`, with matching namespaces.

## Components

| Folder | Surface | Role |
|---|---|---|
| `Images/` | `AddImageProcessing()`, `IImageService` (probe, edit, collage, analyze) | Resize, compress, caption, watermark, collage, BlurHash over SkiaSharp (see `Images/images.md`) |
| `Pdf/` | `AddPdfProcessing()`, `IPdfService` (info, merge, split, pages, images → PDF, metadata, AES-256, text, stamps) | PDF tools over PDFsharp + PdfPig (see `Pdf/pdf.md`) |
| `Word/` | `AddWordProcessing()`, `IWordService` (info, text/Markdown, template fill, Markdown → DOCX) | Word documents over the Open XML SDK (see `Word/word.md`) |
| `Endpoints/` | `MapImageToolEndpoints()`, `MapPdfToolEndpoints()`, `MapWordToolEndpoints()` | The image, PDF and Word tools as multipart HTTP endpoints (see `Endpoints/endpoints.md`) |
| `Markdown/` | `AddMarkdown()`, `IMarkdownService` | Safe GitHub-flavoured HTML, plain text, headings, front matter via Markdig (see `Markdown/markdown.md`) |
| `Captions/` | `AddCaptionParsing()`, `ICaptionParser` (+ VTT/SRT/TTML/json3), `ITimedText` helpers | Parse/convert caption formats; slice parts and render text (see `Captions/captions.md`) |
| `YouTube/` | `YouTubeUrlMapper`, `YouTubeLinkExtractor` | Video and playlist links in any published form, and links in pasted text (see `YouTube/youtube.md`) |
| `Tabular/` | `ITabularExporter`, `ITabularParser`, `TabularFormat` | Shared row export and import (CSV / XLSX), picked by format |
| `Csv/` | `AddCsvExport()`, `CsvTabularExporter`, `ICsvParser` | CSV read + write via CsvHelper |
| `Excel/` | `AddExcelExport()`, `ExcelTabularExporter`, `IExcelParser` | XLSX write and import via ClosedXML |

## Tabular export — quickstart

```csharp
using WoW.Two.Sdk.Backend.Beta.Media.Csv;
using WoW.Two.Sdk.Backend.Beta.Media.Csv.Parsers;
using WoW.Two.Sdk.Backend.Beta.Media.Excel;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular;
using WoW.Two.Sdk.Backend.Beta.Media.Tabular.Exporters;

builder.Services.AddCsvExport().AddExcelExport();

public sealed class Reports(IEnumerable<ITabularExporter> exporters, ICsvParser csv)
{
    public Task ExportAsync(IEnumerable<Invoice> rows, Stream output, TabularFormat format, CancellationToken ct)
    {
        var exporter = exporters.First(e => e.Format == format);   // or inject CsvTabularExporter / ExcelTabularExporter directly
        return exporter.WriteAsync(rows, output, ct);
    }

    public IAsyncEnumerable<Invoice> ImportAsync(Stream csvFile, CancellationToken ct)
        => csv.ReadAsync<Invoice>(csvFile, ct);
}
```

## Notes

- [Tabular export contracts](Tabular/tabular.md) define each format's schema, empty output, stream and cancellation behavior.
- CSV uses invariant default conversion; XLSX stores typed cells in one worksheet. Their column rules differ.
- CSV parsing reads lazily from the current stream position without seeking; enumeration requires the stream to stay open.
- Empty CSV input yields no rows. CsvHelper mapping or data errors propagate during enumeration, possibly after earlier rows were yielded.
- CSV parsing forwards cancellation to CsvHelper. Its reader uses UTF-8 by default with byte-order-mark detection.
- Licenses: CsvHelper (MS-PL/Apache-2.0), ClosedXML (MIT), SkiaSharp (MIT), PDFsharp (MIT), PdfPig (Apache-2.0), Markdig (BSD-2-Clause).

## Roadmap (not yet built)

`Media.Pdf.Rendering` (PDF → images over PDFium) · `Media.Audio`/`Media.Transcripts` (from the TranscriptForge line). ImageSharp and QuestPDF stay out: their licenses carry revenue thresholds.
