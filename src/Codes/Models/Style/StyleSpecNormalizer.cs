namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Provides pre-emit normalization over a <see cref="StyleSpec"/>, returning a render-safe copy rather than throwing so the render path always produces a scannable code.</summary>
/// <remarks>Run before the emitter consumes the spec.</remarks>
public static class StyleSpecNormalizer
{
    /// <summary>Returns a render-safe copy of <paramref name="spec"/> — quiet-zone clamped to the floor, ECC floored to H when a logo is present, and ECC floored to Q when the modules are stylised.</summary>
    /// <param name="spec">The caller-supplied style to normalize.</param>
    public static StyleSpec Normalize(StyleSpec spec)
    {
        var quietZone = Math.Max(QuietZone.MinModules, spec.QuietZoneModules);
        var ecc = ResolveEcc(spec);

        if (quietZone == spec.QuietZoneModules && ecc == spec.EccLevel)
            return spec;

        return spec with { QuietZoneModules = quietZone, EccLevel = ecc };
    }

    /// <summary>Resolves the safe ECC level — a logo forces <see cref="EccLevel.H"/>, otherwise a stylised data body floors ECC to <see cref="EccLevel.Q"/>.</summary>
    /// <param name="spec">The style whose ECC level is being resolved.</param>
    private static EccLevel ResolveEcc(StyleSpec spec)
    {
        // A center logo or emoji occludes modules, so force maximum error correction.
        if (spec.Logo is not null || spec.Emoji is not null)
            return EccLevel.H;

        // A stylised body shrinks the decode margin, so floor ECC to Q; square keeps the caller's choice.
        if (spec.ModuleShape != ModuleShape.Square && spec.EccLevel < EccLevel.Q)
            return EccLevel.Q;

        return spec.EccLevel;
    }
}
