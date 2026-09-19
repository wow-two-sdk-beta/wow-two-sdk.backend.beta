# Tabular export

`ITabularExporter.WriteAsync<T>` writes supplied rows to the selected format. Fetching rows, choosing the
format and delivering the finished file belong to the caller. Implementations live in their owning
`Exporters/` directories; registration remains `AddCsvExport()` or `AddExcelExport()`.

## Schema and representation

The shared interface selects the operation and format. Each implementation uses its library's mapping rules;
the same row type does not necessarily produce the same columns in CSV and XLSX.

| Contract | CSV — CsvHelper | XLSX — ClosedXML |
|---|---|---|
| Ordinary object rows | Public properties are auto-mapped; public fields are excluded by the default configuration. | Public fields and properties are mapped, including static members; indexers are ignored. |
| Header names | Property names by default; member `[Name("...")]` overrides the name. | Member names by default; `[XLColumn(Header = "...")]` overrides the header. |
| Column order | Library auto-map order unless member `[Index(n)]` specifies it. | Library reflection order unless `[XLColumn(Order = n)]` specifies it. |
| Omitted columns | Member `[Ignore]`. | `[XLColumn(Ignore = true)]`. |
| Empty typed object sequence | Header-only CSV. | `Sheet1` with a header-only table. |
| Scalar sequences | Values without a header; an empty scalar sequence writes nothing. | One column with a provider-generated header; integer rows use `Int32`, including an empty sequence. |
| Value representation | Comma-delimited text, UTF-8 without BOM, CRLF records; CsvHelper escapes delimiters and quotes. | XLSX package with one worksheet named `Sheet1` and an Excel table starting at `A1`. |
| Culture | Invariant default conversion. Member conversion/format attributes may customize values. Class-level delimiter/culture attributes are not applied by this writer. | Numbers and dates are stored as typed cells. Their displayed text follows workbook formatting and the reader's locale. |

Unannotated member order is not a stable SDK schema promise. An export whose column order matters supplies
the appropriate member order attributes. There is no shared column-map or cross-format schema normalization.
Other row shapes follow the selected library's support and can fail during mapping or enumeration.
Invariant CSV conversion does not imply ISO timestamps: the default DateTime sample renders
`09/15/2026 00:00:00`. A format attribute or converter defines a different text representation.

## Destination and lifetime

Both exporters leave caller-owned destinations open, including after the observed enumeration,
cancellation and destination-write failures. The caller supplies a writable stream and disposes it.

| Contract | CSV | XLSX |
|---|---|---|
| Position | Writes at the current position without seeking or truncating. Existing prefixes and unwritten suffixes remain. | A readable, seekable destination is rewound and truncated during save. Otherwise the package is copied at the current position without replacing existing contents. |
| Nonseekable or write-only stream | Supported; bytes are written at its current position. | Supported for a fresh destination, including write-only seekable streams. Prefixing unrelated bytes does not produce a standalone workbook. |
| Buffering | Rows are emitted incrementally through writer buffers; the whole document is not materialized. | The complete workbook is built in memory before synchronous saving. Destinations without both reading and seeking support require additional package buffering. |
| Failed row enumeration | Earlier rows and buffered output may already have been written. | Fails during workbook construction, before destination saving starts. |
| Failed destination write | Partial bytes may remain. | Partial package bytes may remain, and existing readable/seekable contents may already have been replaced. |

Exports are not transactional and do not restore the destination after failure. A fresh destination avoids
retained CSV suffixes and prefixed XLSX output. The caller publishes or replaces a final file only after a
successful export when atomic delivery is required. Input size must fit the selected format and its buffering
model; the API does not promise a fixed memory ceiling or an unlimited worksheet size.

## Cancellation

- CSV checks a pre-canceled token before creating writers or writing a header. During enumeration it forwards
  the token to CsvHelper. A CsvHelper wrapper around an observed caller cancellation is unwrapped to preserve
  the original `OperationCanceledException` and its token. Other write/enumeration failures remain failures.
- CSV cancellation can leave partial output, including bytes flushed while writers are disposed. It does not
  interrupt arbitrary synchronous work inside the supplied enumerable or make final buffer flushing cancelable.
- XLSX checks cancellation at entry only. Row enumeration, workbook creation and saving are synchronous;
  cancellation requested during those phases does not interrupt the export. `Task` return type does not offload them.

## Evidence and library controls

Contracts were checked with CsvHelper 33.0.1 and ClosedXML 0.104.2. Runtime observations govern this wrapper's
defaults; library configuration examples can use construction paths that this wrapper does not expose.

- [CsvHelper member attributes](https://joshclose.github.io/CsvHelper/examples/configuration/attributes/).
- [ClosedXML object member selection and ordering](https://docs.closedxml.io/en/0.104.2/features/bulk-insert-data.html).
