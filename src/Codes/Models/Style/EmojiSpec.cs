namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents a center emoji overlaid on the rendered code as SVG text over a knockout halo — a brand mark with no file upload.</summary>
public sealed record EmojiSpec
{
    /// <summary>Gets the emoji glyph rendered at the center.</summary>
    public required string Glyph { get; init; }

    /// <summary>Gets the emoji size as a fraction of the symbol width, clamped to a sane range at emit time.</summary>
    public required double SizeRatio { get; init; }
}
