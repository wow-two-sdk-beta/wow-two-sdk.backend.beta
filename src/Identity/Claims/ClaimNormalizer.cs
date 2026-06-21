using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Per-request <see cref="IClaimsTransformation"/> that adds the canonical <c>wt:*</c> claims by reading <see cref="NormalizedClaimTypes.Provider"/>, looking up that provider's <see cref="ClaimProviderProfile"/>, and copying/deriving from the raw claims already on the principal. Idempotent and non-fabricating.</summary>
public sealed class ClaimNormalizer : IClaimsTransformation
{
    private readonly ClaimNormalizationOptions _options;

    /// <summary>Creates the normalizer from configured options.</summary>
    /// <param name="options">Normalization options (profiles + avatar toggle).</param>
    public ClaimNormalizer(IOptions<ClaimNormalizationOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <summary>Adds the <c>wt:*</c> claims for the principal's provider; returns it unchanged when already normalized, no provider claim exists, or no profile matches.</summary>
    /// <param name="principal">Authenticated principal to normalize.</param>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Idempotent: a second pass (or a re-entrant call) must not duplicate claims.
        if (principal.HasClaim(c => c.Type == NormalizedClaimTypes.UserId))
        {
            return Task.FromResult(principal);
        }

        var provider = principal.FindFirst(NormalizedClaimTypes.Provider)?.Value;
        if (string.IsNullOrEmpty(provider) || !_options.Profiles.TryGetValue(provider, out var profile))
        {
            return Task.FromResult(principal);
        }

        var identity = principal.Identities.FirstOrDefault(i => i.IsAuthenticated) ?? principal.Identities.FirstOrDefault();
        if (identity is null)
        {
            return Task.FromResult(principal);
        }

        var userId = FirstValue(principal, profile.UserIdClaims);
        var username = FirstValue(principal, profile.UsernameClaims);

        Add(identity, NormalizedClaimTypes.UserId, userId);
        Add(identity, NormalizedClaimTypes.Email, FirstValue(principal, profile.EmailClaims));
        Add(identity, NormalizedClaimTypes.DisplayName, FirstValue(principal, profile.DisplayNameClaims));
        Add(identity, NormalizedClaimTypes.Username, username);
        Add(identity, NormalizedClaimTypes.Avatar, ResolveAvatar(principal, profile, userId, username));

        return Task.FromResult(principal);
    }

    /// <summary>First non-empty value among the ordered source claim types, or <c>null</c>.</summary>
    private static string? FirstValue(ClaimsPrincipal principal, IReadOnlyList<string> sourceTypes)
    {
        foreach (var type in sourceTypes)
        {
            var value = principal.FindFirst(type)?.Value;
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Resolves the avatar URL: a direct claim wins, else the profile's synthesizer when enabled.</summary>
    private string? ResolveAvatar(ClaimsPrincipal principal, ClaimProviderProfile profile, string? userId, string? username)
    {
        var direct = FirstValue(principal, profile.AvatarClaims);
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        if (_options.SynthesizeAvatars && profile.AvatarSynthesizer is not null)
        {
            return profile.AvatarSynthesizer(new AvatarSynthesisContext(principal, userId, username));
        }

        return null;
    }

    /// <summary>Adds a canonical claim only when a value resolved — never fabricates an empty claim.</summary>
    private static void Add(ClaimsIdentity identity, string type, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }
}
