using System.Security.Claims;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;
/// <summary>Per-call overrides for one issued token; unset members fall back to <see cref="JwtTokenIssuerOptions"/>.</summary>
public sealed record TokenIssuanceContext
{
    /// <summary>Overrides the configured default lifetime.</summary>
    public TimeSpan? Lifetime { get; init; }

    /// <summary>Overrides the configured default audience.</summary>
    public string? Audience { get; init; }

    /// <summary>Extra JOSE header values (rarely needed).</summary>
    public IReadOnlyDictionary<string, object>? AdditionalHeaders { get; init; }
}
