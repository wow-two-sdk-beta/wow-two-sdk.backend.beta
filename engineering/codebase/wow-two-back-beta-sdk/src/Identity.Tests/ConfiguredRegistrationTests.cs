using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;
using WoW.Two.Sdk.Backend.Beta.Identity.Guest;
using WoW.Two.Sdk.Backend.Beta.Identity.Guest.Services;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests;

/// <summary>Covers the configured path of each options-taking registration — the bare record resolves whether or not a caller supplied a delegate.</summary>
public sealed class ConfiguredRegistrationTests
{
    [Fact]
    public void AddCurrentUser_ShouldResolveTheConfiguredRecord()
    {
        using var provider = new ServiceCollection()
            .AddCurrentUser(options => options.GuestCookieName = "g")
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        provider.GetRequiredService<CurrentUserOptions>().GuestCookieName.Should().Be("g");
        provider.GetRequiredService<ICurrentUserService>().Should().BeOfType<CookieCurrentUserService>();
    }

    [Fact]
    public void CurrentUser_ShouldResolveEachAmbientRequestWithoutRetainingThePreviousUser()
    {
        using var provider = new ServiceCollection().AddCurrentUser().BuildServiceProvider();
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var currentUser = provider.GetRequiredService<ICurrentUserService>();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        accessor.HttpContext = AuthenticatedContext(firstId);
        currentUser.Id.Should().Be(firstId);
        currentUser.Kind.Should().Be(UserKind.User);

        accessor.HttpContext = AuthenticatedContext(secondId);
        provider.GetRequiredService<ICurrentUserService>().Should().BeSameAs(currentUser);
        currentUser.Id.Should().Be(secondId);

        accessor.HttpContext = null;
        currentUser.Id.Should().BeNull();
        currentUser.Kind.Should().Be(UserKind.Anonymous);

        static DefaultHttpContext AuthenticatedContext(Guid id) => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, id.ToString())],
                authenticationType: "test")),
        };
    }

    [Fact]
    public void AddGuestSession_ShouldResolveTheSession_WhenConfigured()
    {
        using var provider = new ServiceCollection().AddGuestSession(_ => { }).BuildServiceProvider();

        provider.GetRequiredService<GuestSessionOptions>().Should().NotBeNull();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IGuestSessionService>().Should().BeOfType<CookieGuestSessionService>();
    }

    [Fact]
    public void AddOtpService_ShouldResolveTheGenerator_WhenConfigured()
    {
        using var provider = new ServiceCollection().AddOtpService(_ => { }).BuildServiceProvider();

        provider.GetRequiredService<OtpOptions>().Should().NotBeNull();
        provider.GetRequiredService<IOtpCodeGenerator>().Should().BeOfType<NumericOtpCodeGenerator>();
    }
}
