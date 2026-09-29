# Images and PDF tools — build track

*Last updated: 2026-09-28*

> Image editing (resize, crop, compress, text, watermark, collage, placeholders) and PDF tooling (pages, security,
> metadata, text, stamps) as SDK modules, so file-tool products and galleries get them from one registration.

## Analysis

- SkiaSharp (MIT) already ships in the core closure for `Codes`, so images add no dependency.
- ImageSharp and QuestPDF carry revenue-threshold licenses; core allows permissive licenses only, so both stay out.
- `ventures/pdf-editor` built PDF infra inline on PDFsharp (MIT) and PdfPig (Apache-2.0): merge, split, rotate,
  watermark, page numbers, encryption, metadata, text. It is the extraction source; its role names follow no keep-list.
- Its font resolver hard-codes OS font paths; SkiaSharp's font manager resolves families on every OS instead.
- PDF → image needs PDFium natives (PDFtoImage, ~10 MB per platform) and belongs in a companion, not core.

## Rulings

- One decode, one encode per call: declarative specs (`ImageEditSpec`, `CollageSpec`) drive a single pass.
- Output formats JPEG, PNG, WebP; inputs anything Skia decodes. Re-encoding drops EXIF, so GPS never leaks.
- Limits guard decompression bombs: `Media:Images:MaxPixels` and `MaxInputBytes`; a refusal is a 400.
- EXIF is read in-house: MetadataExtractor pulls XmpCore under an Adobe EULA, outside core's permissive list.
- Fonts: family through the Skia font manager, configured files by family, per-text glyph fallback (Cyrillic, Uzbek).
- Options: `Media:Images` and `Media:Pdf` through the module-options recipe; code first, host section last.
- PDFsharp 6.2 (net10, AES-256, clean decryption) replaces the venture's 6.1 copy-pages decryption.
- Fonts reach PDFsharp as its fallback resolver: an app's own resolver wins; bold is never simulated.

## Venture adoption (`ventures/pdf-editor`)

- Covered: merge, split, extract, remove, reorder, rotate, watermark, page numbers (and header/footer text), metadata,
  encrypt, decrypt, text, forms.
- Still inline there: Bates numbering, redaction, OCR, DOCX export, DjVu, bulk CSV jobs; flattening stays unbuilt.

## Status

- [x] I1 — image core: probe, auto-orient, crop, resize fits, rotate/flip, encode with quality and a byte budget
- [x] I2 — overlays: text (anchor, wrap, box, shadow, rotation, tiling) and image watermarks; font resolution
- [x] I3 — collage: row, column, grid; gap, padding, background, rounded corners, cover or contain cells
- [x] I4 — analysis: BlurHash placeholder, dominant colors, perceptual hash for duplicates; 22 image tests
- [x] I5 — EXIF metadata: in-house reader (JPEG, PNG, WebP; both byte orders), no dependency; 3 tests
- [x] P1 — PDF core: info, merge, split, extract/reorder, remove, rotate, images → PDF, metadata, AES-256, text
- [x] P2 — PDF stamps: text watermark and page numbers with Skia-resolved fonts (TTC faces extracted); 7 tests
- [x] P4 — compression: JPEG photos downscaled and re-encoded in place, never grown; streams deflated; 1 test
- [x] P5 — forms: read AcroForm fields with options, fill by name (Unicode), lock read-only, refuse unknown names or options
- [x] T1 — tool endpoints: `MapImageToolEndpoints`, `MapPdfToolEndpoints` (multipart in, files or JSON out); 4 tests
- [ ] P3 — PDF → images companion over PDFium [parked: native size]
