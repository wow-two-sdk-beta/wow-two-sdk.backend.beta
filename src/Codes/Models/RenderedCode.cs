using System.Text;
using WoW.Two.Sdk.Backend.Beta.Codes.Models;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Models;

/// <summary>Represents the output of a render — raw bytes plus the content type to serve them with.</summary>
public sealed record RenderedCode(byte[] Content, string ContentType, ImageFormat Format)
{
    /// <summary>Gets the content decoded as UTF-8 text, meaningful for SVG.</summary>
    public string AsText() => Encoding.UTF8.GetString(Content);
}
