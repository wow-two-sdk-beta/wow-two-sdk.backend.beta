# Media.Word

Word (.docx) documents over the Open XML SDK (MIT): read properties, extract text or Markdown, fill templates, and
build documents from Markdown. No Office install and no natives; streams in, bytes out.

## Quick start

```csharp
builder.Services.AddWordProcessing();                                // Media:Word — limits and typography

var info     = await word.ReadInfoAsync(upload);                     // title, author, dates, words, tables, images
var markdown = await word.ExtractTextAsync(upload, WordTextFormat.Markdown);

var letter = await word.FillTemplateAsync(template, new WordTemplateSpec
{
    Data = JsonSerializer.SerializeToElement(new { customer = new { name = "Ada" }, lines = order.Lines }),
});

var report = await word.FromMarkdownAsync(markdownSource, new WordDocumentSpec { Title = "Q3", Paper = WordPaperSize.Letter });
return Results.File(report.Content, report.ContentType, "q3.docx");
```

## Templates

- `{{Customer.Name}}` names a path in the data; names ignore case, numbers index arrays (`{{Lines.0.Sku}}`).
- A table row holding `{{Lines[].Sku}}` repeats once per item; an empty collection removes the row.
- Placeholders may be split across runs, as Word saves them; the value keeps the opening run's formatting.
- Body, headers, footers, footnotes and endnotes are filled; a value's line feeds become line breaks.
- Missing values: `Reject` (default, names them), `Keep` (left visible) or `Empty`.

## Notes

- Markdown extraction keeps headings, lists, emphasis, links, code blocks, quotes and pipe tables.
- Built documents use real styles (Heading 1–6, Quote, Code Block, Table Grid) and numbering; they validate
  against the Office 2019 schema. Only http, https and mailto links become hyperlinks; images become their alt text.
- Refusals raise `WordRejectedException` (`word_unreadable`, `word_too_large`, `word_template_value_missing`),
  a 400 through the media exception rule. Parts over `MaxCharactersPerPart` are refused as XML bombs.
- HTTP: `MapWordToolEndpoints()` — see `../Endpoints/endpoints.md`.
