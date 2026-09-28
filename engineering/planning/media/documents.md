# Document tools — build track

*Last updated: 2026-09-28*

> Reading documents users upload (spreadsheets, Markdown) with the same seams the SDK already writes them through.

## Analysis

- CSV reads and writes; XLSX only wrote, so admin imports (staff lists, catalogs) had no SDK path.
- Markdown renders in help centers, changelogs and blogs; Markdig (BSD-2) is the targets' default.

## Rulings

- Imports mirror exports: one `ITabularParser` per format, picked by `TabularFormat`.
- A cell that does not convert names its row and column (`TabularRowException`) and answers 400.
- Markdown is user content by default: raw HTML escaped, link schemes allow-listed; trusted hosts opt into raw HTML.

## Status

- [x] X1 — XLSX import: `IExcelParser`/`ITabularParser`, forgiving headers, typed conversion, row-precise errors; 4 tests
- [x] M1 — Markdown: safe HTML (raw HTML escaped, unsafe schemes neutralized), plain text, headings, front matter,
  reading time; 4 tests
