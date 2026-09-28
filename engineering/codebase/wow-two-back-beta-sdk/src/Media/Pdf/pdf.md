# Media.Pdf

PDF tools over PDFsharp (MIT; edit, write, protect, stamp) and PdfPig (Apache-2.0; information, text). No native
dependencies. Every call takes streams and returns bytes ready for `Results.File(result.Content, result.ContentType)`.

```csharp
builder.Services.AddPdfProcessing();                                // Media:Pdf in host configuration

var merged   = await pdf.MergeAsync([first, second], ct);
var excerpt  = await pdf.ExtractPagesAsync(file, PdfPageRange.Parse("3,1-2"), ct);   // subset and reorder
var locked   = await pdf.EncryptAsync(file, new PdfEncryptSpec { UserPassword = "open", OwnerPassword = "owner" }, ct);
var numbered = await pdf.AddPageNumbersAsync(file, new PdfPageNumberSpec { Format = "Page {page} of {total}" }, ct);
var album    = await pdf.FromImagesAsync(photos, new PdfImagesSpec { PageSize = PdfPageSize.A4 }, ct);
var text     = await pdf.ExtractTextAsync(file, cancellationToken: ct);
```

| Call | Does |
|---|---|
| `ReadInfoAsync` | page count, sizes and rotation, version, encryption, title/author/… |
| `MergeAsync` · `SplitAsync` | join in order · cut into parts of N pages |
| `ExtractPagesAsync` · `RemovePagesAsync` · `RotatePagesAsync` | subset/reorder · drop · turn by 90° steps |
| `FromImagesAsync` | one upright image per page (EXIF applied, photos re-encoded), A4/Letter/Legal/A3/A5 or image size |
| `UpdateMetadataAsync` | title, author, subject, keywords, creator |
| `EncryptAsync` · `DecryptAsync` | AES-256 with open/owner passwords and permissions · removal keeping outlines and information |
| `ExtractTextAsync` | reading-order text per page; scanned pages without a text layer read empty |
| `AddWatermarkAsync` · `AddPageNumbersAsync` | diagonal text stamp · `{page}`/`{total}` template at an anchor |

- Page lists: `PdfPageRange.Parse("1-3, 5, 8-")`; a descending run such as `5-3` reads backwards.
- Fonts: `Media:Pdf:Fonts:{family}` files, else platform families through SkiaSharp; `.ttc` faces are extracted.
  Installed as PDFsharp's fallback resolver, so an application's own resolver still wins. Bold is never simulated.
- Limits: `MaxInputBytes`, `MaxPages`. Refusals are `PdfRejectedException` with a `Reason` (`pdf_unreadable`,
  `pdf_password_required`, `pdf_password_invalid`, `pdf_page_range_invalid`, `pdf_empty`, `pdf_too_large`) → 400.
- Stamps draw in unrotated page space; on a page with `/Rotate` they turn with it.
- Not here: PDF → images needs PDFium natives and waits for a companion package.
