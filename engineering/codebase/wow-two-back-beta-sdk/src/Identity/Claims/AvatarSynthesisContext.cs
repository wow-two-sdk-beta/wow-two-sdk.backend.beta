using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Inputs for a <see cref="ClaimProviderSpec.AvatarSynthesizer"/>: the principal plus the canonical values already resolved this pass.</summary>
public readonly record struct AvatarSynthesisContext
{
    /// <summary>Principal being normalized — read raw provider claims from here.</summary>
    public required ClaimsPrincipal Principal { get; init; }

    /// <summary>Resolved <see cref="NormalizedClaimTypeConstants.UserId"/>, or <c>null</c>.</summary>
    public required string? UserId { get; init; }

    /// <summary>Resolved <see cref="NormalizedClaimTypeConstants.Username"/>, or <c>null</c>.</summary>
    public required string? Username { get; init; }
}
