using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt;
using WoW.Two.Sdk.Backend.Beta.Identity.Jwt.Issuance;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests;

/// <summary>Verifies JWT trust-source, metadata, algorithm and key-length enforcement.</summary>
public sealed class JwtRegistrationTests
{
    [Fact]
    public void BearerRegistration_ShouldRejectMissingIssuerOrAudience()
    {
        var missingIssuer = () => new ServiceCollection().AddJwtBearerAuthentication(options =>
        {
            options.Audience = "audience";
            options.SymmetricKey = new string('k', 32);
        });
        missingIssuer.Should().Throw<InvalidOperationException>().WithMessage("*Issuer*");

        var missingAudience = () => new ServiceCollection().AddJwtBearerAuthentication(options =>
        {
            options.Issuer = "issuer";
            options.SymmetricKey = new string('k', 32);
        });
        missingAudience.Should().Throw<InvalidOperationException>().WithMessage("*Audience*");
    }

    [Fact]
    public void BearerRegistration_ShouldRejectMissingOrSimultaneousKeySources()
    {
        var missing = () => RegisterBearer(_ => { });
        missing.Should().Throw<InvalidOperationException>().WithMessage("*exactly one*");

        var simultaneous = () => RegisterBearer(options =>
        {
            options.SymmetricKey = new string('k', 32);
            options.MetadataAddress = new Uri("https://identity.example/.well-known/openid-configuration");
        });
        simultaneous.Should().Throw<InvalidOperationException>().WithMessage("*exactly one*");
    }

    [Theory]
    [InlineData(SecurityAlgorithms.HmacSha256, 31)]
    [InlineData(SecurityAlgorithms.HmacSha384, 47)]
    [InlineData(SecurityAlgorithms.HmacSha512, 63)]
    public void BearerRegistration_ShouldRejectKeysShorterThanTheSelectedAlgorithm(
        string algorithm, int keyLength)
    {
        var register = () => RegisterBearer(options =>
        {
            options.Algorithm = algorithm;
            options.SymmetricKey = new string('k', keyLength);
        });

        register.Should().Throw<InvalidOperationException>().WithMessage("*UTF-8 bytes*");
    }

    [Fact]
    public void BearerRegistration_ShouldRequireHttpsMetadataUnlessDevelopmentEscapeIsExplicit()
    {
        var insecure = () => RegisterBearer(options =>
        {
            options.Algorithm = SecurityAlgorithms.RsaSha256;
            options.MetadataAddress = new Uri("http://localhost:8080/.well-known/openid-configuration");
        });
        insecure.Should().Throw<InvalidOperationException>().WithMessage("*must use HTTPS*");

        using var provider = RegisterBearer(options =>
        {
            options.Algorithm = SecurityAlgorithms.RsaSha256;
            options.MetadataAddress = new Uri("http://localhost:8080/.well-known/openid-configuration");
            options.AllowInsecureMetadataForDevelopment = true;
        });
        var bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        bearer.RequireHttpsMetadata.Should().BeFalse();
    }

    [Fact]
    public void BearerRegistration_ShouldPinMetadataAlgorithmAndHttps()
    {
        using var provider = RegisterBearer(options =>
        {
            options.Algorithm = SecurityAlgorithms.RsaSha256;
            options.MetadataAddress = new Uri("https://identity.example/.well-known/openid-configuration");
        });

        var bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        bearer.RequireHttpsMetadata.Should().BeTrue();
        bearer.MetadataAddress.Should().Be("https://identity.example/.well-known/openid-configuration");
        bearer.TokenValidationParameters.ValidAlgorithms.Should().Equal(SecurityAlgorithms.RsaSha256);
    }

    [Fact]
    public void BearerRegistration_ShouldPinSymmetricAlgorithm()
    {
        using var provider = RegisterBearer(options =>
        {
            options.Algorithm = SecurityAlgorithms.HmacSha256;
            options.SymmetricKey = new string('k', 32);
        });

        var bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        bearer.TokenValidationParameters.ValidAlgorithms.Should().Equal(SecurityAlgorithms.HmacSha256);
        bearer.TokenValidationParameters.IssuerSigningKey.Should().BeOfType<SymmetricSecurityKey>();
    }

    [Fact]
    public void BearerRegistration_ShouldRejectSymmetricAlgorithmForMetadata()
    {
        var register = () => RegisterBearer(options =>
        {
            options.Algorithm = SecurityAlgorithms.HmacSha256;
            options.MetadataAddress = new Uri("https://identity.example/.well-known/openid-configuration");
        });

        register.Should().Throw<InvalidOperationException>().WithMessage("*requires RS256*");
    }

    [Theory]
    [InlineData("HS256", 31)]
    [InlineData("HS384", 47)]
    [InlineData("HS512", 63)]
    public void IssuanceRegistration_ShouldRejectKeysShorterThanTheSelectedAlgorithm(
        string algorithm, int keyLength)
    {
        using var provider = new ServiceCollection()
            .AddJwtTokenIssuance(options =>
            {
                options.Issuer = "issuer";
                options.Audience = "audience";
                options.Algorithm = algorithm;
                options.SigningKey = new string('k', keyLength);
            })
            .BuildServiceProvider();

        var resolve = () => provider.GetRequiredService<JwtTokenIssuerOptions>();
        resolve.Should().Throw<OptionsValidationException>().WithMessage("*shorter*");
    }

    private static ServiceProvider RegisterBearer(Action<JwtOptions> configure) =>
        new ServiceCollection()
            .AddJwtBearerAuthentication(options =>
            {
                options.Issuer = "issuer";
                options.Audience = "audience";
                configure(options);
            })
            .BuildServiceProvider();
}
