using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering;

/// <summary>Defines the contract for rendering 1D/2D barcodes to SVG.</summary>
public interface IBarcodeRenderer
{
    /// <summary>Renders the payload in the given symbology as an SVG string.</summary>
    /// <param name="payload">The data to encode.</param>
    /// <param name="format">The barcode symbology to render.</param>
    /// <param name="style">The style to render with.</param>
    string RenderSvg(string payload, BarcodeFormat format, StyleSpec style);
}
