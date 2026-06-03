using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt;

/// <summary>JWT bearer registration options.</summary>
public sealed record JwtOptions
{
    /// <summary>Required token issuer.</summary>
    public string Issuer { get; init; } = "";

    /// <summary>Required token audience.</summary>
    public string Audience { get; init; } = "";

    /// <summary>Symmetric signing key (for HMAC algos). Use either this or <see cref="JwksUri"/>.</summary>
    public string? SymmetricKey { get; init; }

    /// <summary>JWKS URI (for asymmetric / managed keys via OIDC discovery).</summary>
    public Uri? JwksUri { get; init; }

    /// <summary>Validate token expiration. Default <c>true</c>.</summary>
    public bool ValidateLifetime { get; init; } = true;

    /// <summary>Clock skew. Default 30 seconds.</summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>JWT bearer registration helpers.</summary>
public static class JwtServiceCollectionExtensions
{
    /// <summary>Register JWT bearer authentication using the supplied options.</summary>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services, Action<JwtOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var opts = new JwtOptions();
        configure(opts);

        if (string.IsNullOrWhiteSpace(opts.Issuer)) throw new InvalidOperationException("JwtOptions.Issuer is required.");
        if (string.IsNullOrWhiteSpace(opts.Audience)) throw new InvalidOperationException("JwtOptions.Audience is required.");
        if (string.IsNullOrWhiteSpace(opts.SymmetricKey) && opts.JwksUri is null)
            throw new InvalidOperationException("Either JwtOptions.SymmetricKey or JwtOptions.JwksUri must be supplied.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, b =>
            {
                b.RequireHttpsMetadata = true;
                b.SaveToken = true;
                b.MapInboundClaims = false;
                b.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = opts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = opts.Audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = opts.ValidateLifetime,
                    ClockSkew = opts.ClockSkew,
                    IssuerSigningKey = opts.SymmetricKey is { Length: > 0 }
                        ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SymmetricKey))
                        : null,
                };
                if (opts.JwksUri is not null)
                {
                    b.MetadataAddress = opts.JwksUri.ToString();
                    b.RequireHttpsMetadata = opts.JwksUri.Scheme == Uri.UriSchemeHttps;
                }
            });

        return services;
    }
}
