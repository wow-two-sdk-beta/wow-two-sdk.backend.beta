using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Claims;

/// <summary>Maps provider claims to the SDK's canonical claims.</summary>
public sealed class ClaimMapper : IClaimsTransformation
{
    private readonly ClaimNormalizationOptions _options;

    /// <summary>Creates the normalizer from configured options.</summary>
    /// <param name="options">Normalization options (specs and avatar toggle).</param>
    public ClaimMapper(ClaimNormalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>Adds the <c>wt:*</c> claims for the principal's provider; returns it unchanged when already normalized, no provider claim exists, or no spec matches.</summary>
    /// <param name="principal">Authenticated principal to normalize.</param>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // Idempotent: a second pass (or a re-entrant call) must not duplicate claims.
        if (principal.HasClaim(c => c.Type == NormalizedClaimTypeConstants.UserId))
        {
            return Task.FromResult(principal);
        }

        var provider = principal.FindFirst(NormalizedClaimTypeConstants.Provider)?.Value;
        if (string.IsNullOrEmpty(provider) || !_options.Specs.TryGetValue(provider, out var spec))
        {
            return Task.FromResult(principal);
        }

        var identity = principal.Identities.FirstOrDefault(i => i.IsAuthenticated) ?? principal.Identities.FirstOrDefault();
        if (identity is null)
        {
            return Task.FromResult(principal);
        }

        var userId = FirstValue(principal, spec.UserIdClaims);
        var username = FirstValue(principal, spec.UsernameClaims);

        Add(identity, NormalizedClaimTypeConstants.UserId, userId);
        Add(identity, NormalizedClaimTypeConstants.Email, FirstValue(principal, spec.EmailClaims));
        Add(identity, NormalizedClaimTypeConstants.DisplayName, FirstValue(principal, spec.DisplayNameClaims));
        Add(identity, NormalizedClaimTypeConstants.Username, username);
        Add(identity, NormalizedClaimTypeConstants.Avatar, ResolveAvatar(principal, spec, userId, username));

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

    /// <summary>Resolves the avatar URL: a direct claim wins, else the spec's synthesizer when enabled.</summary>
    private string? ResolveAvatar(ClaimsPrincipal principal, ClaimProviderSpec spec, string? userId, string? username)
    {
        var direct = FirstValue(principal, spec.AvatarClaims);
        if (!string.IsNullOrEmpty(direct))
        {
            return direct;
        }

        if (_options.SynthesizeAvatars && spec.AvatarSynthesizer is not null)
        {
            return spec.AvatarSynthesizer(new AvatarSynthesisContext { Principal = principal, UserId = userId, Username = username });
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
