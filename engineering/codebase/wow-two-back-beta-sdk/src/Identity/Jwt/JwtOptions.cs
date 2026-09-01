using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt;

/// <summary>JWT bearer registration options.</summary>
public sealed record JwtOptions
{
    /// <summary>Required token issuer.</summary>
    public string Issuer { get; set; } = "";

    /// <summary>Required token audience.</summary>
    public string Audience { get; set; } = "";

    /// <summary>Symmetric signing key (for HMAC algos). Use either this or <see cref="JwksUri"/>.</summary>
    public string? SymmetricKey { get; set; }

    /// <summary>JWKS URI (for asymmetric / managed keys via OIDC discovery).</summary>
    public Uri? JwksUri { get; set; }

    /// <summary>Validate token expiration. Default <c>true</c>.</summary>
    public bool ValidateLifetime { get; set; } = true;

    /// <summary>Clock skew. Default 30 seconds.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);
}
