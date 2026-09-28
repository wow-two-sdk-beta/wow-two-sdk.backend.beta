namespace WoW.Two.Sdk.Backend.Beta.Identity.Core.Endpoints;

/// <summary>Fresh recovery codes, shown once; earlier codes stop working.</summary>
public sealed record RecoveryCodesDto
{
    /// <summary>The codes.</summary>
    public required IReadOnlyList<string> RecoveryCodes { get; init; }
}
