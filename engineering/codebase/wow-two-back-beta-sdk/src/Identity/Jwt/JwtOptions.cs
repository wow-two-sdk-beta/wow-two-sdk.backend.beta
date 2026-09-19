using Microsoft.IdentityModel.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt;

/// <summary>Holds jWT bearer registration options.</summary>
public sealed record JwtOptions
{
    /// <summary>Required token issuer.</summary>
    public string Issuer { get; set; } = "";

    /// <summary>Required token audience.</summary>
    public string Audience { get; set; } = "";

    /// <summary>Gets or sets the symmetric verification key. Configure exactly one of this property and <see cref="MetadataAddress"/>.</summary>
    public string? SymmetricKey { get; set; }

    /// <summary>Gets or sets the OpenID Connect discovery metadata address. Configure exactly one of this property and <see cref="SymmetricKey"/>.</summary>
    public Uri? MetadataAddress { get; set; }

    /// <summary>Gets or sets the only accepted JWT signing algorithm. Default <c>HS256</c>.</summary>
    public string Algorithm { get; set; } = SecurityAlgorithms.HmacSha256;

    /// <summary>Gets or sets whether HTTP metadata is allowed for an explicit local-development setup. Default <c>false</c>.</summary>
    public bool AllowInsecureMetadataForDevelopment { get; set; }

    /// <summary>Validate token expiration. Default <c>true</c>.</summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>Clock skew. Default 30 seconds.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
}
