namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents a foreground gradient that replaces the solid <see cref="StyleSpec.ForegroundColor"/> across the data body and finder eyes; ignored unless it carries at least two <see cref="Stops"/>.</summary>
public sealed record GradientSpec
{
    /// <summary>Gets the gradient projection — linear along <see cref="Angle"/>, or radial from the center.</summary>
    public required GradientType Type { get; init; }

    /// <summary>Gets the ordered color stops (offsets run 0..1); a gradient with fewer than two stops falls back to the solid foreground.</summary>
    public required IReadOnlyList<GradientStopSpec> Stops { get; init; }

    /// <summary>Gets the linear-gradient angle in degrees (0 = left→right, 90 = top→bottom); ignored for <see cref="GradientType.Radial"/>.</summary>
    public required double Angle { get; init; }
}
