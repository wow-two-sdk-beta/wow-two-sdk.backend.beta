namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Defines the geometry of the outer 7×7 frame of each finder pattern, rendered independently of <see cref="ModuleShape"/> so the data body can be stylised while the eyes stay crisp.</summary>
public enum FinderShape
{
    /// <summary>A square ring — the standard finder frame and the default.</summary>
    Square,

    /// <summary>A rounded-rect ring — softened corners on the outer frame.</summary>
    Rounded,

    /// <summary>A circular ring (annulus) — the outer frame as a hollow circle.</summary>
    Circle,
}
