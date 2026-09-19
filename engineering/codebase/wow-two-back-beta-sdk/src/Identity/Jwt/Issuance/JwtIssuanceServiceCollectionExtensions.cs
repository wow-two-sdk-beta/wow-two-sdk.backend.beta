using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text;
using WoW.Two.Sdk.Backend.Beta.Foundation.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;

/// <summary>JWT issuance registration.</summary>
public static class JwtIssuanceServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ITokenIssuer"/> issuing symmetric (HMAC) JWTs; the sibling <c>AddJwtBearerAuthentication</c> validates them — keep both on the same key.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Issuer / audience / lifetime / signing key.</param>
    public static IServiceCollection AddJwtTokenIssuance(
        this IServiceCollection services,
        Action<JwtTokenIssuerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddValidatedOptions<JwtTokenIssuerOptions>(
            configure,
            builder => builder
                .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "JwtTokenIssuerOptions.Issuer must not be empty.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "JwtTokenIssuerOptions.Audience must not be empty.")
                .Validate(options => options.Lifetime > TimeSpan.Zero, "JwtTokenIssuerOptions.Lifetime must be positive.")
                .Validate(options => HasRequiredKeyLength(options), "JwtTokenIssuerOptions.SigningKey is shorter than the selected HMAC algorithm requires (HS256: 32 bytes, HS384: 48 bytes, HS512: 64 bytes).")
                .Validate(options => options.Algorithm is "HS256" or "HS384" or "HS512", "JwtTokenIssuerOptions.Algorithm must be HS256, HS384 or HS512."));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ITokenIssuer, JwtTokenIssuer>();
        return services;
    }

    private static bool HasRequiredKeyLength(JwtTokenIssuerOptions options)
    {
        var requiredBytes = options.Algorithm switch
        {
            "HS256" => 32,
            "HS384" => 48,
            "HS512" => 64,
            _ => int.MaxValue,
        };
        return !string.IsNullOrEmpty(options.SigningKey)
            && Encoding.UTF8.GetByteCount(options.SigningKey) >= requiredBytes;
    }
}
