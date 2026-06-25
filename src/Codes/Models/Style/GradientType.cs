namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Defines how a <see cref="GradientSpec"/> projects its color stops across the foreground.</summary>
public enum GradientType
{
    /// <summary>A straight-line gradient along <see cref="GradientSpec.Angle"/>.</summary>
    Linear,

    /// <summary>A radial gradient spreading from the symbol center outward.</summary>
    Radial,
}
