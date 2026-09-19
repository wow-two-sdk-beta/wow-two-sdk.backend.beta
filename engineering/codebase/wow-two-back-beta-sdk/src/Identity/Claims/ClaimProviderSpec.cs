using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Declares how one provider's raw claims map onto the canonical <c>wt:*</c> set; each field lists source claim types in priority order (first match wins).</summary>
public sealed record ClaimProviderSpec
{
    /// <summary>Auth scheme this spec applies to (e.g. <c>"GitHub"</c>); matched case-insensitively.</summary>
    public required string Scheme { get; init; }

    /// <summary>Priority-ordered sources for <see cref="NormalizedClaimTypeConstants.UserId"/>.</summary>
    public required IReadOnlyList<string> UserIdClaims { get; init; }

    /// <summary>Priority-ordered sources for <see cref="NormalizedClaimTypeConstants.Email"/>.</summary>
    public required IReadOnlyList<string> EmailClaims { get; init; }

    /// <summary>Priority-ordered sources for <see cref="NormalizedClaimTypeConstants.DisplayName"/>.</summary>
    public required IReadOnlyList<string> DisplayNameClaims { get; init; }

    /// <summary>Priority-ordered sources for <see cref="NormalizedClaimTypeConstants.Username"/>.</summary>
    public required IReadOnlyList<string> UsernameClaims { get; init; }

    /// <summary>Priority-ordered sources for a ready avatar URL; empty when synth-only or unavailable.</summary>
    public required IReadOnlyList<string> AvatarClaims { get; init; }

    /// <summary>Optional fallback deriving an avatar URL from resolved values when no <see cref="AvatarClaims"/> claim is present; honored only when avatar synthesis is enabled.</summary>
    public Func<AvatarSynthesisContext, string?>? AvatarSynthesizer { get; init; }
}
