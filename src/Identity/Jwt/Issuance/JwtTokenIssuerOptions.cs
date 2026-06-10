namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;

/// <summary>
/// Settings for symmetric JWT issuance. Pair with the sibling <c>AddJwtBearerAuthentication</c>
/// (validation) using the same key so issued tokens validate across services.
/// </summary>
public sealed class JwtTokenIssuerOptions
{
    /// <summary>Value of the <c>iss</c> claim.</summary>
    public string Issuer { get; set; } = "";

    /// <summary>Default value of the <c>aud</c> claim (overridable per call).</summary>
    public string Audience { get; set; } = "";

    /// <summary>Default token lifetime (overridable per call). Default 1h.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Symmetric signing key. Source from a secret store — never hard-code.
    /// HS256 needs at least 32 bytes of key material.
    /// </summary>
    public string SigningKey { get; set; } = "";

    /// <summary>Signing algorithm: <c>HS256</c> (default), <c>HS384</c>, or <c>HS512</c>. Asymmetric (RS*/ES*) is a future extension.</summary>
    public string Algorithm { get; set; } = "HS256";
}
