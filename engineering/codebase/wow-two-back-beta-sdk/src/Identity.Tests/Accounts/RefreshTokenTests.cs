using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.RefreshTokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Refresh tokens: rotation, reuse detection, expiry, stamp rotation and revocation.</summary>
public sealed class RefreshTokenTests
{
    private static AccountsHost CreateHost() => AccountsHost.Create(identity => identity.AddRefreshTokens(o => o.Lifetime = TimeSpan.FromDays(7)));

    [Fact]
    public async Task Redeem_ShouldRotateAndRevokeTheFamilyWhenASpentTokenReturns()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("walt");
        await using var scope = host.Scope();
        var refresh = scope.ServiceProvider.GetRequiredService<RefreshTokenService<IdentityUser, Guid>>();

        var first = await refresh.IssueAsync(user);
        first.ExpiresAt.Should().Be(host.Time.GetUtcNow().AddDays(7));

        var rotated = await refresh.RedeemAsync(first.Token);
        rotated.Succeeded.Should().BeTrue();
        rotated.User!.Id.Should().Be(user.Id);
        rotated.Token!.Token.Should().NotBe(first.Token);

        (await refresh.RedeemAsync(first.Token)).Status.Should().Be(RefreshTokenStatus.Reused);
        (await refresh.RedeemAsync(rotated.Token.Token)).Status.Should().Be(RefreshTokenStatus.Invalid);
    }

    [Fact]
    public async Task Redeem_ShouldRejectTamperingExpiryAndARotatedStamp()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("xena");
        await using var scope = host.Scope();
        var refresh = scope.ServiceProvider.GetRequiredService<RefreshTokenService<IdentityUser, Guid>>();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>();

        var token = (await refresh.IssueAsync(user)).Token;
        (await refresh.RedeemAsync(token[..^3] + "AAA")).Status.Should().Be(RefreshTokenStatus.Invalid);
        (await refresh.RedeemAsync("garbage")).Status.Should().Be(RefreshTokenStatus.Invalid);

        host.Time.Advance(TimeSpan.FromDays(7));
        (await refresh.RedeemAsync(token)).Status.Should().Be(RefreshTokenStatus.Expired);

        var live = (await refresh.IssueAsync(user)).Token;
        await accounts.RotateSecurityStampAsync((await accounts.FindByIdAsync(user.Id))!);
        (await refresh.RedeemAsync(live)).Status.Should().Be(RefreshTokenStatus.Invalid);
        (await refresh.PurgeExpiredAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Revoke_ShouldEndOneFamilyAndRevokeAllShouldEndEvery()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("yuri");
        await using var scope = host.Scope();
        var refresh = scope.ServiceProvider.GetRequiredService<RefreshTokenService<IdentityUser, Guid>>();

        var phone = (await refresh.IssueAsync(user)).Token;
        var laptop = (await refresh.IssueAsync(user)).Token;
        await refresh.RevokeAsync(phone);
        (await refresh.RedeemAsync(phone)).Status.Should().Be(RefreshTokenStatus.Invalid);

        var next = await refresh.RedeemAsync(laptop);
        next.Succeeded.Should().BeTrue();
        await refresh.RevokeAllAsync(user);
        (await refresh.RedeemAsync(next.Token!.Token)).Status.Should().Be(RefreshTokenStatus.Invalid);
    }
}
