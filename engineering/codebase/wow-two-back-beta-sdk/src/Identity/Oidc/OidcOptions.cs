using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Oidc;

/// <summary>OIDC registration options.</summary>
public sealed record OidcOptions
{
    /// <summary>OIDC authority (issuer URL — discovery is loaded from `{authority}/.well-known/openid-configuration`).</summary>
    public string Authority { get; set; } = "";

    /// <summary>Client id.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>Client secret (omit for public PKCE-only clients).</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Scopes to request. Default: openid profile email.</summary>
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];

    /// <summary>Save tokens in the auth ticket. Default <c>true</c>.</summary>
    public bool SaveTokens { get; set; } = true;
}
