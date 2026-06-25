# Rendering

The server-authoritative render path: `matrix → StyleSpec → SVG → (raster) PNG`. One source for SVG, PNG, and the live preview — preview == export by construction.

## Pipeline

```
IQrMatrixGenerator (QRCoder, quiet-zone stripped)
   → SvgRenderer (StyleSpec → SVG; module + finder shapes, fg/bg, transparency, quiet zone, <image> logo)
      → ISvgRasterizer (Svg.Skia/SkiaSharp → PNG ≥300 DPI)
```

- `QrCodeRenderer` orchestrates the three; `CodeRenderer` is the QR-vs-barcode facade.
- Barcodes are plain ZXing SVG (unify under the emitter later).
- `StyleSpecNormalizer` runs pre-emit: quiet-zone floor (4), ECC floor to H when a logo is present, ECC floor to Q for a stylised module body.

## StyleSpec extensibility

`SvgRenderer.EmitStyledForeground` / `AppendPerModuleBody` is the per-module shape seam. Future styling (gradients, frames) lands as new nullable `StyleSpec` sub-records consumed at that seam — additive, no new code path. `StyleSpec.Default` reproduces the QRCoder output byte-for-byte (the regression gate).

## Docker / Linux requirement (raster path)

SkiaSharp needs its native lib **and** fontconfig on Linux:

1. `SkiaSharp.NativeAssets.Linux` — referenced by the mono-lib (`WoW.Two.Sdk.Backend.Beta.csproj`, Codes ItemGroup; ships `libSkiaSharp.so` for linux-x64/arm64/musl into any Linux publish). Pinned to the SkiaSharp version `Svg.Skia` drags in (3.119.2).
2. **`libfontconfig1`** — NOT in the `mcr.microsoft.com/dotnet/aspnet` base image. Without it, `SKImageInfo`'s static ctor throws on first raster (`sk_colortype_get_default_8888`). The image **must** install it:

```dockerfile
RUN apt-get update && apt-get install -y --no-install-recommends libfontconfig1 && rm -rf /var/lib/apt/lists/*
```

Verified: a framework-dependent linux-x64 publish runs the full `SvgRenderer → SkiaSvgRasterizer` path and emits a valid PNG inside `dotnet/aspnet:10.0` once `libfontconfig1` is present. Any **consumer** that publishes the raster path to Linux must add the line above to that service's Dockerfile — this library ships the native `.so` but cannot install OS packages.
