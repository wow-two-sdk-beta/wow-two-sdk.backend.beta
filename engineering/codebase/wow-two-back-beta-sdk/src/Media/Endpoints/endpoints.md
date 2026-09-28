# Media.Endpoints

The image and PDF tools as multipart HTTP endpoints — a file-tool product is registration plus one line of routing.

```csharp
builder.Services.AddPdfProcessing();                               // images come with it
var tools = app.MapGroup("/tools");
tools.MapImageToolEndpoints().RequireRateLimiting("uploads");      // each call returns its group
tools.MapPdfToolEndpoints();
```

| Route | Form fields | Answer |
|---|---|---|
| `images/probe` · `images/metadata` · `images/analyze` | `file` | JSON (`ApiResponse`) |
| `images/edit` | `file`, `spec` (`ImageEditSpec` JSON), optional `watermark` file | the image |
| `images/collage` | `files`…, `spec` (`CollageSpec` JSON) | the collage |
| `pdf/info` · `pdf/text` | `file`, optional `password`, `pages` (text) | JSON |
| `pdf/merge` · `pdf/from-images` | `files`…, `spec` (`PdfImagesSpec`, from-images) | the PDF |
| `pdf/split` | `file`, `pagesPerPart` | a ZIP of parts |
| `pdf/extract` · `pdf/remove` · `pdf/rotate` | `file`, `pages` (`1-3,5`), `degrees` (rotate) | the PDF |
| `pdf/compress` · `pdf/metadata` · `pdf/encrypt` · `pdf/watermark` · `pdf/page-numbers` | `file`, `spec` JSON | the PDF |
| `pdf/decrypt` | `file`, `password` | the PDF |
| `pdf/form` · `pdf/form/fill` | `file`; fill adds `spec` (`{"values":{…},"lockFields":true}`) | JSON fields · the PDF |

- Specs are the C# records as JSON in camelCase, enums as names: `{"resize":{"width":800},"output":{"format":"webp"}}`.
- Downloads keep the upload's name with the new extension (`photo.jpg` → `photo.webp`).
- Refusals and malformed specs answer 400 problem details with the refusal's `messageKey`.
- Antiforgery is off for these groups (API clients); request size limits stay the host's (Kestrel, `FormOptions`).
