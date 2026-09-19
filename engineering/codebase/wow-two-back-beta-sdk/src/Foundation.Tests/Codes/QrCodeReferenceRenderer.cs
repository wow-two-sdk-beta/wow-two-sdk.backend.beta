using Xunit;
using QRCoder;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Codes;

/// <summary>Renders live-computed QRCoder <c>SvgQRCode</c> output as a parity reference.</summary>
internal sealed class QrCodeReferenceRenderer
{
    /// <summary>Renders <paramref name="payload"/> at ECC Q.</summary>
    public string Svg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var renderer = new SvgQRCode(data);
        return renderer.GetGraphic(20, "#000000", "#FFFFFF");
    }
}
