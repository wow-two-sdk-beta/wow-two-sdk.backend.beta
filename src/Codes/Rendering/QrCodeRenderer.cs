using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Matrix;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Raster;
using WoW.Two.Sdk.Backend.Beta.Codes.Rendering.Svg;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering;

/// <summary>Provides the server-authoritative QR render path — a matrix source produces the grid, <see cref="SvgRenderer"/> styles it to SVG, and <see cref="ISvgRasterizer"/> rasterizes that SVG to PNG.</summary>
public sealed class QrCodeRenderer(
    IQrMatrixGenerator matrixGenerator,
    SvgRenderer emitter,
    ISvgRasterizer rasterizer) : IQrCodeRenderer
{
    /// <inheritdoc />
    public string RenderSvg(string payload, StyleSpec style)
    {
        var normalized = StyleSpecNormalizer.Normalize(style);
        var matrix = matrixGenerator.Generate(payload, normalized.EccLevel);
        return emitter.Emit(matrix, normalized);
    }

    /// <inheritdoc />
    public byte[] RenderPng(string payload, StyleSpec style)
    {
        var svg = RenderSvg(payload, style);
        return rasterizer.ToPng(svg);
    }
}
