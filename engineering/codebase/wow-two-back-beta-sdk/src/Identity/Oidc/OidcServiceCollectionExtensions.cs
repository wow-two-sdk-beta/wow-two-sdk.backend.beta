using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Oidc;

/// <summary>OIDC registration helpers.</summary>
public static class OidcServiceCollectionExtensions
{
    /// <summary>Register OIDC with Authorization Code + PKCE — pairs with <c>AddCookieAuthentication</c> for the sign-in cookie.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configuration of the OIDC options.</param>
    public static IServiceCollection AddOpenIdConnectAuthentication(this IServiceCollection services, Action<OidcOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var opts = new OidcOptions();
        configure(opts);

        if (string.IsNullOrWhiteSpace(opts.Authority)) throw new InvalidOperationException("OidcOptions.Authority is required.");
        if (string.IsNullOrWhiteSpace(opts.ClientId)) throw new InvalidOperationException("OidcOptions.ClientId is required.");

        services.AddAuthentication(b =>
        {
            b.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            b.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddOpenIdConnect(o =>
        {
            o.Authority = opts.Authority;
            o.ClientId = opts.ClientId;
            o.ClientSecret = opts.ClientSecret;
            o.ResponseType = OpenIdConnectResponseType.Code;
            o.UsePkce = true;
            o.SaveTokens = opts.SaveTokens;
            o.MapInboundClaims = false;
            o.GetClaimsFromUserInfoEndpoint = true;
            o.Scope.Clear();
            foreach (var s in opts.Scopes) o.Scope.Add(s);
        });

        return services;
    }
}
