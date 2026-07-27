namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents a radial foreground gradient whose color stops spread from the symbol center outward to <see cref="Radius"/>.</summary>
public sealed record RadialGradientSpec : GradientSpec
{
    /// <summary>Gets the radial extent as a fraction (0..1) of the canvas half-size — smaller pulls the end color toward the center (tighter). Defaults to <c>1.0</c> (full extent).</summary>
    public double Radius { get; init; } = 1.0;
}
