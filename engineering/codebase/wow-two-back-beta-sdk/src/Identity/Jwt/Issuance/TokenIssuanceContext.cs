using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;
/// <summary>Per-call overrides for one issued token; unset members fall back to <see cref="JwtTokenIssuerOptions"/>.</summary>
/// <param name="Lifetime">Overrides the configured default lifetime.</param>
/// <param name="Audience">Overrides the configured default audience.</param>
/// <param name="AdditionalHeaders">Extra JOSE header values (rarely needed).</param>
public sealed record TokenIssuanceContext(
    TimeSpan? Lifetime = null,
    string? Audience = null,
    IReadOnlyDictionary<string, object>? AdditionalHeaders = null);
