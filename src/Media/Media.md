# Media

*Media & document processing — caption parsing and tabular (CSV/Excel) export. Own domain logic + thin wraps over permissive libs.*

Namespace root: `WoW.Two.Sdk.Backend.Beta.Media`.

## Components

| Folder | Surface | Role |
|---|---|---|
| `Captions/` | `AddCaptionParsing()`, `ICaptionParser` (+ VTT/SRT/TTML/json3) | Parse/convert caption formats (see `Captions/captions.md`) |
| `Tabular/` | `ITabularExporter`, `TabularFormat` | Shared row-export abstraction (CSV / XLSX) |
| `Csv/` | `AddCsvExport()`, `CsvTabularExporter`, `ICsvReader` | CSV read + write via CsvHelper |
| `Excel/` | `AddExcelExport()`, `ExcelTabularExporter` | XLSX write via ClosedXML |

## Tabular export — quickstart

```csharp
builder.Services.AddCsvExport().AddExcelExport();

public sealed class Reports(IEnumerable<ITabularExporter> exporters, ICsvReader csv)
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

- Row public properties become columns; a header row is written. Streams are left open for the caller to dispose.
- CSV uses invariant culture (stable machine format). Excel writes one worksheet as a table.
- Licenses: CsvHelper (MS-PL/Apache-2.0), ClosedXML (MIT).

## Roadmap (not yet built)

`Media.QuestPdf` (PDF) · `Media.Markdig` (Markdown) · `Media.ImageSharp` (images) · `Media.Audio`/`Media.Transcripts` (from the TranscriptForge line).
