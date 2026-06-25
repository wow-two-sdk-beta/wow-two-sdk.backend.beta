using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Models;

/// <summary>Represents a request to render a single code image.</summary>
public sealed record CodeRenderRequest
{
    /// <summary>Gets the data to encode — typically the short redirect URL for a dynamic code.</summary>
    public required string Payload { get; init; }

    /// <summary>Gets the symbology to render.</summary>
    public required BarcodeFormat Symbology { get; init; }

    /// <summary>Gets the output image format.</summary>
    public required ImageFormat Format { get; init; }

    /// <summary>Gets the style contract the emitter consumes.</summary>
    public required StyleSpec Style { get; init; }
}
