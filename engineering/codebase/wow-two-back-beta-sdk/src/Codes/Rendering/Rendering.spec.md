# Code rendering contract

- SVG and PNG are supported for every declared BarcodeFormat. Unknown enum values fail validation.
- Style validation is external and reusable through the SDK IValidator adapters. Render entry points enforce the same rules.
- Colors are six-digit hexadecimal RGB. Quiet zones are 0..32 modules before normalization (floor 4).
- Logo and emoji ratios are finite in (0,1]; emitter clamping retains existing appearance semantics.
- Gradients have at most 32 ordered stops, offsets 0..1, angle -360..360 and finite radius; radius is (0,1].
- Inline logos accept base64 PNG/JPEG only, at most 1 MiB decoded and 4096 pixels per side / 16 million pixels.
- Payloads are nonempty, at most 8192 UTF-16 characters, and must fit the chosen symbology. Validation failures do not echo payloads.
- SVG rasterization accepts the code engine's SVG subset, not arbitrary active SVG. No DTD, scripts, stylesheets, external references or fonts.
- Raster output is bounded to 4096 pixels per side and 16 million pixels. Source SVG is bounded to 4 MiB of characters.
- ICodeRenderer.Render returns Result<RenderedCode>, with ValidationError for rejected input or symbology capacity.
- Lower-level rendering methods use the documented ValidateAndThrow bridge; native allocation/encoding failures yield InvalidOperationException.
- Normalization and valid existing SVG geometry retain their existing behavior. Barcode styling remains outside this cut.
