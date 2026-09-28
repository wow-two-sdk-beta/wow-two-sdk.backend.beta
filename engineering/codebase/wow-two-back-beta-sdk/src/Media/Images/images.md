# Media.Images

Raster image tools over SkiaSharp (MIT, already in the core closure): edit, compress, caption, watermark, collage and
summarize. Each call decodes once and encodes once; re-encoding drops EXIF, so GPS and camera data never leak.

```csharp
builder.Services.AddImageProcessing();                              // Media:Images in host configuration

var thumb = await images.EditAsync(upload, new ImageEditSpec
{
    Resize = new ImageResizeSpec { Width = 800, Height = 800, Fit = ImageFit.Cover },
    Texts = [new TextOverlaySpec { Text = "© Acme", Anchor = ImageAnchor.BottomRight, Shadow = true }],
    Output = new ImageOutputSpec { Format = ImageFormat.Webp, MaxBytes = 150_000 },
}, ct);
return Results.File(thumb.Content, thumb.ContentType);

var duo = await images.CollageAsync([left, right], new CollageSpec { Layout = CollageLayout.Row, CornerRadius = 24 }, ct);
var summary = await images.AnalyzeAsync(upload, ct);               // BlurHash, colors, perceptual hash
```

| Call | Does |
|---|---|
| `ProbeAsync` | format, upright size, EXIF orientation, alpha, frames — header only |
| `EditAsync` | orient → crop → rotate/flip → resize (`Max`, `Cover`, `Pad`, `Stretch`) → watermark → texts → encode |
| `CollageAsync` | `Row` (duo, strip), `Column`, `Grid` (last row centered); gap, padding, background, rounded cells |
| `ReadMetadataAsync` | EXIF from JPEG, PNG, WebP: taken at (+offset), camera, lens, exposure, f-number, ISO, GPS |
| `AnalyzeAsync` | BlurHash (4×3), average and dominant colors, 64-bit difference hash |

- Compression: `Quality` for JPEG/WebP; `MaxBytes` lowers quality to `MinQuality`, then shrinks, for every format.
- Texts wrap at `MaxWidth`, sit at an anchor or `Tile` across the image; sizes are shares of the width by default.
- Fonts: `FontFamily` → `Media:Images:Fonts:{family}` file → platform family → a family that has the glyphs.
- Duplicates: `hash.Distance(other)` counts differing bits; up to about 10 is the same picture.
- Metadata: read before editing (edits drop EXIF); `HasLocation` flags photos that would leak where they were taken.
  HEIC/AVIF containers are not read.
- Limits: `MaxInputBytes`, `MaxPixels` (decompression bombs), `MaxOutputDimension`; refusals are
  `ImageRejectedException` with a `Reason`, mapped to a 400 with that `messageKey`.
- Writes JPEG, PNG and WebP; reads what the platform decodes (GIF first frame, BMP, ICO; HEIF/AVIF where supported).
- Linux containers need `libfontconfig1` and at least one font package (`fonts-dejavu-core`) for texts.
