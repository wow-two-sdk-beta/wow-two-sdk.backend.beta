namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents one color stop of a <see cref="GradientSpec"/>.</summary>
public sealed record GradientStopSpec
{
    /// <summary>Gets the stop color as <c>#RRGGBB</c>.</summary>
    public required string Color { get; init; }

    /// <summary>Gets the stop position along the gradient, 0..1.</summary>
    public required double Offset { get; init; }
}
