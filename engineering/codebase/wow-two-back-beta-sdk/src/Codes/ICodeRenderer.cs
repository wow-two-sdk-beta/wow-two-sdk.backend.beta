using WoW.Two.Sdk.Backend.Beta.Codes.Models;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace WoW.Two.Sdk.Backend.Beta.Codes;

/// <summary>Defines rendering a code (QR or barcode) to SVG or PNG — the single entry point consumed by services.</summary>
public interface ICodeRenderer
{
    /// <summary>Renders the requested code image.</summary>
    /// <param name="request">The render request describing the payload, symbology, format, and style.</param>
    Result<RenderedCode> Render(CodeRenderRequest request);
}
