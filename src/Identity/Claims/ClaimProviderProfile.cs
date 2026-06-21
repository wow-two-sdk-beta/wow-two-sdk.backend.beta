using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Maps one OAuth provider's raw claims onto the canonical <c>wt:*</c> set; each field lists accepted source claim types in priority order (first match wins) to absorb the OAuth2 (<see cref="ClaimTypes"/> URIs) vs OIDC (short names) duality.</summary>
/// <param name="Scheme">Auth scheme this profile applies to (e.g. <c>"GitHub"</c>); matched case-insensitively.</param>
/// <param name="UserIdClaims">Priority-ordered sources for <see cref="NormalizedClaimTypes.UserId"/>.</param>
/// <param name="EmailClaims">Priority-ordered sources for <see cref="NormalizedClaimTypes.Email"/>.</param>
/// <param name="DisplayNameClaims">Priority-ordered sources for <see cref="NormalizedClaimTypes.DisplayName"/>.</param>
/// <param name="UsernameClaims">Priority-ordered sources for <see cref="NormalizedClaimTypes.Username"/>.</param>
/// <param name="AvatarClaims">Priority-ordered sources for a ready avatar URL; empty when synth-only or unavailable.</param>
/// <param name="AvatarSynthesizer">Optional fallback deriving an avatar URL from resolved values when no <paramref name="AvatarClaims"/> claim is present; honored only when avatar synthesis is enabled.</param>
public sealed record ClaimProviderProfile(
    string Scheme,
    IReadOnlyList<string> UserIdClaims,
    IReadOnlyList<string> EmailClaims,
    IReadOnlyList<string> DisplayNameClaims,
    IReadOnlyList<string> UsernameClaims,
    IReadOnlyList<string> AvatarClaims,
    Func<AvatarSynthesisContext, string?>? AvatarSynthesizer = null);

/// <summary>Inputs for a <see cref="ClaimProviderProfile.AvatarSynthesizer"/>: the principal plus the canonical values already resolved this pass.</summary>
/// <param name="Principal">Principal being normalized — read raw provider claims from here.</param>
/// <param name="UserId">Resolved <see cref="NormalizedClaimTypes.UserId"/>, or <c>null</c>.</param>
/// <param name="Username">Resolved <see cref="NormalizedClaimTypes.Username"/>, or <c>null</c>.</param>
public readonly record struct AvatarSynthesisContext(
    ClaimsPrincipal Principal,
    string? UserId,
    string? Username);
