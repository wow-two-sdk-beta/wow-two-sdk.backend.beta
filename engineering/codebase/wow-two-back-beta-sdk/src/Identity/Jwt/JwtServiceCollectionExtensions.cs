using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt;
/// <summary>JWT bearer registration helpers.</summary>
public static class JwtServiceCollectionExtensions
{
    /// <summary>Register JWT bearer authentication using the supplied options.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Configures the JWT bearer options (issuer, audience, signing key, lifetime).</param>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services, Action<JwtOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var opts = new JwtOptions();
        configure(opts);

        if (string.IsNullOrWhiteSpace(opts.Issuer)) throw new InvalidOperationException("JwtOptions.Issuer is required.");
        if (string.IsNullOrWhiteSpace(opts.Audience)) throw new InvalidOperationException("JwtOptions.Audience is required.");
        ValidateTrustConfiguration(opts);

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
                    ValidAlgorithms = [opts.Algorithm],
                    IssuerSigningKey = opts.SymmetricKey is { Length: > 0 }
                        ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SymmetricKey))
                        : null,
                };
                if (opts.MetadataAddress is not null)
                {
                    b.MetadataAddress = opts.MetadataAddress.ToString();
                    b.RequireHttpsMetadata = !opts.AllowInsecureMetadataForDevelopment;
                }
            });

        return services;
    }

    private static void ValidateTrustConfiguration(JwtOptions options)
    {
        var hasSymmetricKey = !string.IsNullOrWhiteSpace(options.SymmetricKey);
        var hasMetadata = options.MetadataAddress is not null;
        if (hasSymmetricKey == hasMetadata)
        {
            throw new InvalidOperationException(
                "Configure exactly one JWT verification-key source: JwtOptions.SymmetricKey or JwtOptions.MetadataAddress.");
        }

        if (hasSymmetricKey)
        {
            var requiredBytes = options.Algorithm switch
            {
                SecurityAlgorithms.HmacSha256 => 32,
                SecurityAlgorithms.HmacSha384 => 48,
                SecurityAlgorithms.HmacSha512 => 64,
                _ => throw new InvalidOperationException(
                    "Symmetric JWT validation supports HS256, HS384 or HS512."),
            };

            if (Encoding.UTF8.GetByteCount(options.SymmetricKey!) < requiredBytes)
            {
                throw new InvalidOperationException(
                    $"JwtOptions.SymmetricKey must contain at least {requiredBytes} UTF-8 bytes for {options.Algorithm}.");
            }

            return;
        }

        if (!options.MetadataAddress!.IsAbsoluteUri)
            throw new InvalidOperationException("JwtOptions.MetadataAddress must be an absolute URI.");
        if (options.MetadataAddress.Scheme != Uri.UriSchemeHttps && !options.AllowInsecureMetadataForDevelopment)
        {
            throw new InvalidOperationException(
                "JwtOptions.MetadataAddress must use HTTPS unless AllowInsecureMetadataForDevelopment is explicitly enabled.");
        }
        if (!IsAsymmetricAlgorithm(options.Algorithm))
        {
            throw new InvalidOperationException(
                "Metadata-based JWT validation requires RS256/384/512, PS256/384/512 or ES256/384/512.");
        }
    }

    private static bool IsAsymmetricAlgorithm(string algorithm) => algorithm is
        SecurityAlgorithms.RsaSha256 or
        SecurityAlgorithms.RsaSha384 or
        SecurityAlgorithms.RsaSha512 or
        SecurityAlgorithms.RsaSsaPssSha256 or
        SecurityAlgorithms.RsaSsaPssSha384 or
        SecurityAlgorithms.RsaSsaPssSha512 or
        SecurityAlgorithms.EcdsaSha256 or
        SecurityAlgorithms.EcdsaSha384 or
        SecurityAlgorithms.EcdsaSha512;
}
