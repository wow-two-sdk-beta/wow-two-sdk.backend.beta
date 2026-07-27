using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes;

/// <summary>Defines the contract for rendering a code (QR or barcode) to SVG or PNG — the single entry point consumed by services.</summary>
public interface ICodeRenderer
{
    /// <summary>Renders the requested code image.</summary>
    /// <param name="request">The render request describing the payload, symbology, format, and style.</param>
    RenderedCode Render(CodeRenderRequest request);
}
