# Document tools — build track

*Last updated: 2026-09-28*

> Reading documents users upload (spreadsheets, Markdown, Word) with the same seams the SDK already writes them through.

## Analysis

- CSV reads and writes; XLSX only wrote, so admin imports (staff lists, catalogs) had no SDK path.
- Markdown renders in help centers, changelogs and blogs; Markdig (BSD-2) is the targets' default.
- Word: contracts, letters and reports are .docx; the Open XML SDK (MIT) already ships with ClosedXML.

## Rulings

- Imports mirror exports: one `ITabularParser` per format, picked by `TabularFormat`.
- A cell that does not convert names its row and column (`TabularRowException`) and answers 400.
- Markdown is user content by default: raw HTML escaped, link schemes allow-listed; trusted hosts opt into raw HTML.
- Word templates use `{{path}}` placeholders over JSON data and `{{Items[].Field}}` row repeats; no conditionals yet.
- Missing template values are refused by default, so a letter never ships with a hole.

## Status

- [x] X1 — XLSX import: `IExcelParser`/`ITabularParser`, forgiving headers, typed conversion, row-precise errors; 4 tests
- [x] M1 — Markdown: safe HTML (raw HTML escaped, unsafe schemes neutralized), plain text, headings, front matter,
  reading time; 4 tests
- [x] W1 — Word: info, text and Markdown extraction, split-run template fill with row repeats, Markdown → DOCX with
  real styles and numbering (Office 2019 schema-valid); `word/*` endpoints; 5 tests
- [ ] W2 — Word template conditionals and loops over paragraphs (`{{#if}}`, `{{#each}}` blocks)
