namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents a center logo overlaid on the rendered code as an <c>&lt;image&gt;</c>.</summary>
public sealed record LogoSpec
{
    /// <summary>Gets the logo as a data URL (e.g. <c>data:image/png;base64,…</c>), embedded directly into the SVG's <c>&lt;image&gt;</c> href.</summary>
    public required string DataUrl { get; init; }

    /// <summary>Gets the logo width as a fraction of the symbol width, clamped to a sane range at emit time.</summary>
    public required double SizeRatio { get; init; }
}
