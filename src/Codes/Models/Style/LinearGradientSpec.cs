namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents a straight-line foreground gradient whose color stops run along <see cref="Angle"/>.</summary>
public sealed record LinearGradientSpec : GradientSpec
{
    /// <summary>Gets the gradient angle in degrees (0 = left→right, 90 = top→bottom), applied about the canvas center.</summary>
    public required double Angle { get; init; }
}
