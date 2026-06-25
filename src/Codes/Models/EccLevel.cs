namespace WoW.Two.Sdk.Backend.Beta.Codes.Models;

/// <summary>Defines the QR error-correction level — higher levels add redundancy that tolerates a center logo or damage at the cost of density.</summary>
public enum EccLevel
{
    /// <summary>About 7% recovery.</summary>
    L,

    /// <summary>About 15% recovery.</summary>
    M,

    /// <summary>About 25% recovery — safe for a center logo.</summary>
    Q,

    /// <summary>About 30% recovery — use with large logos.</summary>
    H,
}
