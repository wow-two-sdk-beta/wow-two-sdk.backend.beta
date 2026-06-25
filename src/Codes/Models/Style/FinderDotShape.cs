namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Defines the geometry of the inner 3×3 pupil of each finder pattern, independent of the outer <see cref="FinderShape"/> and of <see cref="ModuleShape"/>.</summary>
public enum FinderDotShape
{
    /// <summary>A solid square pupil — the default.</summary>
    Square,

    /// <summary>A rounded-rect pupil.</summary>
    Rounded,

    /// <summary>A solid circular pupil.</summary>
    Circle,
}
