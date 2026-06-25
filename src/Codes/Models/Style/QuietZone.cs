namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Contains the quiet-zone constants — the blank margin a scanner needs to lock onto the symbol.</summary>
public static class QuietZone
{
    /// <summary>The minimum quiet-zone width in modules (ISO/IEC 18004 specifies 4 for QR); narrower requests are clamped up.</summary>
    public const int MinModules = 4;
}
