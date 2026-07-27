using WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Rendering;

/// <summary>Defines the contract for rendering QR codes from a <see cref="StyleSpec"/> to SVG and PNG.</summary>
public interface IQrCodeRenderer
{
    /// <summary>Renders the payload as an SVG string off the emitter.</summary>
    /// <param name="payload">The data to encode.</param>
    /// <param name="style">The style to render with.</param>
    string RenderSvg(string payload, StyleSpec style);

    /// <summary>Renders the payload as PNG bytes by rasterizing the emitter's SVG.</summary>
    /// <param name="payload">The data to encode.</param>
    /// <param name="style">The style to render with.</param>
    byte[] RenderPng(string payload, StyleSpec style);
}
