namespace WoW.Two.Sdk.Backend.Beta.Codes.Models.Style;

/// <summary>Represents the versioned style contract the emitter consumes — the single input that fully describes a code's appearance, persisted as camelCase JSON in <c>CodeEntity.StyleJson</c>.</summary>
/// <remarks>Add a new styling feature as a new field (typically its own nested sub-record), never a new code path; bump <see cref="CurrentSchemaVersion"/> on a non-additive change. Construct the render-default via <see cref="Default"/>.</remarks>
public sealed record StyleSpec
{
    /// <summary>The current schema version, incremented on a non-additive change to any field's meaning.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Gets the schema version of this spec, letting the deserializer migrate an older persisted shape forward.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets the foreground (dark module) color as <c>#RRGGBB</c>.</summary>
    public required string ForegroundColor { get; init; }

    /// <summary>Gets the background color as <c>#RRGGBB</c>, ignored when <see cref="TransparentBackground"/> is set.</summary>
    public required string BackgroundColor { get; init; }

    /// <summary>Gets whether the background rectangle is omitted, rendering the code on a transparent canvas.</summary>
    public required bool TransparentBackground { get; init; }

    /// <summary>Gets the optional foreground gradient; when set (≥2 stops) it replaces <see cref="ForegroundColor"/> across the data body and finder eyes.</summary>
    public GradientSpec? Gradient { get; init; }

    /// <summary>Gets the error-correction level, floored to <see cref="EccLevel.H"/> by the normalizer when a <see cref="Logo"/> is present.</summary>
    public required EccLevel EccLevel { get; init; }

    /// <summary>Gets the quiet-zone width in modules around the symbol, floored to <see cref="QuietZone.MinModules"/> by the normalizer.</summary>
    public required int QuietZoneModules { get; init; }

    /// <summary>Gets the optional center logo overlay, forcing ECC to <see cref="EccLevel.H"/> when set.</summary>
    public LogoSpec? Logo { get; init; }

    /// <summary>Gets the optional center emoji overlay, forcing ECC to <see cref="EccLevel.H"/> when set.</summary>
    public EmojiSpec? Emoji { get; init; }

    /// <summary>Gets the geometry of the data modules (the code body, excluding finder eyes); a non-<see cref="ModuleShape.Square"/> value floors ECC to <see cref="EccLevel.Q"/>.</summary>
    public required ModuleShape ModuleShape { get; init; }

    /// <summary>Gets the geometry of the outer 7×7 finder frames, independent of <see cref="ModuleShape"/>.</summary>
    public required FinderShape FinderShape { get; init; }

    /// <summary>Gets the geometry of the inner 3×3 finder pupils, independent of <see cref="FinderShape"/> and <see cref="ModuleShape"/>.</summary>
    public required FinderDotShape FinderDotShape { get; init; }

    /// <summary>Gets the render-default spec — solid black on white, ECC Q, quiet zone 4, square geometry, no logo — the byte-parity baseline.</summary>
    public static StyleSpec Default { get; } = new()
    {
        ForegroundColor = "#000000",
        BackgroundColor = "#FFFFFF",
        TransparentBackground = false,
        EccLevel = EccLevel.Q,
        QuietZoneModules = QuietZone.MinModules,
        ModuleShape = ModuleShape.Square,
        FinderShape = FinderShape.Square,
        FinderDotShape = FinderDotShape.Square,
    };
}
