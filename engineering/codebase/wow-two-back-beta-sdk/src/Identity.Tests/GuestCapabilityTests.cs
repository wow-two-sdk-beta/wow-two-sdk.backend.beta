using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.CurrentUser;
using WoW.Two.Sdk.Backend.Beta.Identity.Guest;
using WoW.Two.Sdk.Backend.Beta.Identity.Guest.Services;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests;

public sealed class GuestCapabilityTests
{
    [Fact]
    public void ProvisionReuseClearAndReprovision_AgreeWithinAndAcrossRequests()
    {
        using var provider = Services();
        var first = Request(provider);
        using var scope = provider.CreateScope();
        var guest = scope.ServiceProvider.GetRequiredService<IGuestSessionService>();
        var user = provider.GetRequiredService<ICurrentUserService>();
        var id = guest.EnsureGuest();
        Assert.Equal(id, guest.EnsureGuest());
        Assert.Equal(id, user.Id);
        Assert.Equal(UserKind.Guest, user.Kind);
        Assert.Single(first.Response.Headers.SetCookie);
        var cookie = first.Response.Headers.SetCookie.ToString().Split(';')[0];
        Assert.DoesNotContain(id.ToString(), cookie);
        Assert.Contains("secure", first.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", first.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);

        Request(provider, cookie);
        Assert.Equal(id, user.Id);
        using var secondScope = provider.CreateScope();
        var second = secondScope.ServiceProvider.GetRequiredService<IGuestSessionService>();
        Assert.Equal(id, second.EnsureGuest());
        second.Clear();
        Assert.Null(user.Id);
        Assert.Equal(UserKind.Anonymous, user.Kind);
        var replacement = second.EnsureGuest();
        Assert.NotEqual(id, replacement);
        Assert.Equal(replacement, user.Id);
    }

    [Theory]
    [InlineData("not-a-capability")]
    [InlineData("c0e5f064-8d00-4c64-a7e1-679eb9b2eaa7")]
    public void RawAndMalformedCookies_AreAnonymous(string token)
    {
        using var provider = Services();
        Request(provider, "user-id=" + token);
        Assert.Null(provider.GetRequiredService<ICurrentUserService>().Id);
        Assert.Equal(UserKind.Anonymous, provider.GetRequiredService<ICurrentUserService>().Kind);
    }

    [Fact]
    public void TamperingExpirationCookiePurposeAndKeyIsolation_AreRejected()
    {
        var clock = new ManualTimeProvider();
        var protection = new EphemeralDataProtectionProvider();
        using var provider = Services(clock, protection);
        var http = Request(provider);
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IGuestSessionService>().EnsureGuest();
        var token = http.Response.Headers.SetCookie.ToString().Split(';')[0].Split('=', 2)[1];
        var user = provider.GetRequiredService<ICurrentUserService>();
        Request(provider, "user-id=" + token[..20] + (token[20] == 'A' ? 'B' : 'A') + token[21..]);
        Assert.Null(user.Id);
        using var otherName = Services(clock, protection, "other-guest");
        Request(otherName, "other-guest=" + token);
        Assert.Null(otherName.GetRequiredService<ICurrentUserService>().Id);
        using var otherKeys = Services(clock);
        Request(otherKeys, "user-id=" + token);
        Assert.Null(otherKeys.GetRequiredService<ICurrentUserService>().Id);
        clock.Now += TimeSpan.FromHours(1);
        Request(provider, "user-id=" + token);
        Assert.Null(user.Id);
    }

    [Fact]
    public void RegisteredIdentity_TakesPrecedenceAndDoesNotLeakAcrossRequests()
    {
        using var provider = Services();
        var http = Request(provider);
        var accountId = Guid.NewGuid();
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, accountId.ToString())], "test"));
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IGuestSessionService>().EnsureGuest();
        var user = provider.GetRequiredService<ICurrentUserService>();
        Assert.Equal(accountId, user.Id);
        Assert.Equal(UserKind.User, user.Kind);
        scope.ServiceProvider.GetRequiredService<IGuestSessionService>().Clear();
        Assert.Equal(accountId, user.Id);
        Request(provider);
        Assert.Null(user.Id);
        Assert.Equal(UserKind.Anonymous, user.Kind);
    }

    private static ServiceProvider Services(ManualTimeProvider? clock = null, IDataProtectionProvider? protection = null, string name = "user-id")
    {
        var services = new ServiceCollection();
        services.AddSingleton(protection ?? new EphemeralDataProtectionProvider());
        services.AddSingleton<TimeProvider>(clock ?? new ManualTimeProvider());
        services.AddGuestSession(o => { o.CookieName = name; o.Lifetime = TimeSpan.FromHours(1); });
        services.AddCurrentUser(o => o.GuestCookieName = name);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static DefaultHttpContext Request(ServiceProvider provider, string? cookie = null)
    {
        var http = new DefaultHttpContext();
        if (cookie is not null) http.Request.Headers.Cookie = cookie;
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        return http;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 19, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
