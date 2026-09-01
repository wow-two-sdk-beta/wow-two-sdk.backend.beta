using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Inputs for a <see cref="ClaimProviderProfile.AvatarSynthesizer"/>: the principal plus the canonical values already resolved this pass.</summary>
/// <param name="Principal">Principal being normalized — read raw provider claims from here.</param>
/// <param name="UserId">Resolved <see cref="NormalizedClaimTypeConstants.UserId"/>, or <c>null</c>.</param>
/// <param name="Username">Resolved <see cref="NormalizedClaimTypeConstants.Username"/>, or <c>null</c>.</param>
public readonly record struct AvatarSynthesisContext(
    ClaimsPrincipal Principal,
    string? UserId,
    string? Username);
