using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents a foreground gradient that replaces the solid <see cref="StyleSpec.ForegroundColor"/> across the data body and finder eyes; the polymorphic base carries the shared color stops, with the projection (<see cref="LinearGradientSpec"/> or <see cref="RadialGradientSpec"/>) discriminated by the camelCase <c>type</c> — mirror the frontend union when adding a variant.</summary>
/// <remarks>Ignored unless it carries at least two <see cref="Stops"/>; a gradient with fewer falls back to the solid foreground.</remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LinearGradientSpec), "linear")]
[JsonDerivedType(typeof(RadialGradientSpec), "radial")]
public abstract record GradientSpec
{
    /// <summary>Gets the ordered color stops (offsets run 0..1); a gradient with fewer than two stops falls back to the solid foreground.</summary>
    public required IReadOnlyList<GradientStopSpec> Stops { get; init; }
}
