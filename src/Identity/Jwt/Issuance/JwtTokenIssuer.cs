using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;

/// <summary>
/// Default <see cref="ITokenIssuer"/> — symmetric (HMAC) JWTs via <see cref="JsonWebTokenHandler"/>.
/// Pure library: usable from workers and console apps without ASP.NET Core.
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    private readonly JwtTokenIssuerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _signingCredentials;

    /// <summary>Creates the issuer from configured options.</summary>
    /// <param name="options">Issuer / audience / lifetime / signing key settings.</param>
    /// <param name="timeProvider">Time source for <c>iat</c> / <c>nbf</c> / <c>exp</c>.</param>
    public JwtTokenIssuer(IOptions<JwtTokenIssuerOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options.Value;
        _timeProvider = timeProvider;

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("JwtTokenIssuerOptions.SigningKey must be configured.");
        }

        var algorithm = _options.Algorithm switch
        {
            "HS256" => SecurityAlgorithms.HmacSha256,
            "HS384" => SecurityAlgorithms.HmacSha384,
            "HS512" => SecurityAlgorithms.HmacSha512,
            _ => throw new InvalidOperationException(
                $"Unsupported signing algorithm '{_options.Algorithm}'. Supported: HS256, HS384, HS512."),
        };

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            algorithm);
    }

    /// <inheritdoc />
    public string Issue(IEnumerable<Claim> claims, TokenIssuanceContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = context?.Audience ?? _options.Audience,
            NotBefore = now,
            IssuedAt = now,
            Expires = now + (context?.Lifetime ?? _options.Lifetime),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = _signingCredentials,
        };

        if (context?.AdditionalHeaders is { Count: > 0 } headers)
        {
            descriptor.AdditionalHeaderClaims = headers.ToDictionary(h => h.Key, h => h.Value, StringComparer.Ordinal);
        }

        return Handler.CreateToken(descriptor);
    }
}
