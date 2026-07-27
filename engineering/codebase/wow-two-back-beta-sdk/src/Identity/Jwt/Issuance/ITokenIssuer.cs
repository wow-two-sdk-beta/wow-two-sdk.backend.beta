using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;

/// <summary>Issues signed tokens from caller-supplied claims; the SDK never constructs claims.</summary>
public interface ITokenIssuer
{
    /// <summary>Issues a signed token carrying <paramref name="claims"/>.</summary>
    /// <param name="claims">Claims to embed (e.g. <c>sub</c>, roles, custom claims).</param>
    /// <param name="context">Optional per-call overrides of lifetime / audience.</param>
    string Issue(IEnumerable<Claim> claims, TokenIssuanceContext? context = null);
}

/// <summary>Per-call overrides for one issued token; unset members fall back to <see cref="JwtTokenIssuerOptions"/>.</summary>
/// <param name="Lifetime">Overrides the configured default lifetime.</param>
/// <param name="Audience">Overrides the configured default audience.</param>
/// <param name="AdditionalHeaders">Extra JOSE header values (rarely needed).</param>
public sealed record TokenIssuanceContext(
    TimeSpan? Lifetime = null,
    string? Audience = null,
    IReadOnlyDictionary<string, object>? AdditionalHeaders = null);
