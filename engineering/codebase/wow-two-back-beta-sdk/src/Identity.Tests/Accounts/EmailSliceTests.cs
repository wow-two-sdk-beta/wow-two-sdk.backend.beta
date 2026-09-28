using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Emails;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Purpose tokens and the email slice: binding, tampering, expiry, stamp rotation and address scoping.</summary>
public sealed class EmailSliceTests
{
    private static AccountsHost CreateHost() => AccountsHost.Create(identity => identity
        .AddUserTokens(o => o.SigningKey = AccountsHost.SigningKey)
        .AddEmailConfirmation());

    [Fact]
    public async Task Tokens_ShouldRejectTamperingAnotherPurposeAnotherUserAndARotatedStamp()
    {
        await using var host = CreateHost();
        var alice = await host.CreateUserAsync("alice");
        var bob = await host.CreateUserAsync("bob");
        var issuer = host.Provider.GetRequiredService<UserTokenIssuer<IdentityUser, Guid>>();

        var token = issuer.Issue(alice, "Invite");

        issuer.Verify(alice, "Invite", token).Should().BeTrue();
        issuer.Verify(alice, "Other", token).Should().BeFalse();
        issuer.Verify(bob, "Invite", token).Should().BeFalse();
        issuer.Verify(alice, "Invite", token[..^2] + (token[^2] == 'A' ? "B" : "A") + token[^1]).Should().BeFalse();
        issuer.Verify(alice, "Invite", "not-a-token").Should().BeFalse();

        alice.SecurityStamp = "rotated";
        issuer.Verify(alice, "Invite", token).Should().BeFalse();
    }

    [Fact]
    public async Task Tokens_ShouldExpireAfterThePurposeLifetime()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("carl");
        var issuer = host.Provider.GetRequiredService<UserTokenIssuer<IdentityUser, Guid>>();
        var token = issuer.Issue(user, "Custom");

        host.Time.Advance(TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1));
        issuer.Verify(user, "Custom", token).Should().BeTrue();
        host.Time.Advance(TimeSpan.FromSeconds(1));
        issuer.Verify(user, "Custom", token).Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmEmail_ShouldOnlyAcceptATokenForTheCurrentAddress()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("dina");
        await using var scope = host.Scope();
        var emails = scope.ServiceProvider.GetRequiredService<UserEmailService<IdentityUser, Guid>>();
        var tracked = (await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(user.Id))!;
        var token = emails.IssueConfirmationToken(tracked);

        (await emails.SetEmailAsync(tracked, "dina.new@example.test")).Succeeded.Should().BeTrue();
        (await emails.ConfirmEmailAsync(tracked, token)).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidToken);

        (await emails.ConfirmEmailAsync(tracked, emails.IssueConfirmationToken(tracked))).Succeeded.Should().BeTrue();
        (await host.ReloadAsync(user.Id)).EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEmail_ShouldMoveToTheTokenAddressAndRejectATakenOne()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("ella");
        await host.CreateUserAsync("fred", "fred@example.test");
        await using var scope = host.Scope();
        var emails = scope.ServiceProvider.GetRequiredService<UserEmailService<IdentityUser, Guid>>();
        var tracked = (await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(user.Id))!;
        var stamp = tracked.SecurityStamp;

        var taken = emails.IssueChangeEmailToken(tracked, "FRED@example.test");
        (await emails.ChangeEmailAsync(tracked, "fred@example.test", taken)).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.DuplicateEmail);

        var token = emails.IssueChangeEmailToken(tracked, "ella.moved@example.test");
        (await emails.ChangeEmailAsync(tracked, "other@example.test", token)).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidToken);
        (await emails.ChangeEmailAsync(tracked, "Ella.Moved@example.test", token)).Succeeded.Should().BeTrue();

        var stored = await host.ReloadAsync(user.Id);
        stored.NormalizedEmail.Should().Be("ELLA.MOVED@EXAMPLE.TEST");
        stored.EmailConfirmed.Should().BeTrue();
        stored.SecurityStamp.Should().NotBe(stamp);
    }

    [Fact]
    public void AddUserTokens_ShouldRejectAShortKeyAtStart()
    {
        var act = () => AccountsHost.Create(identity => identity.AddUserTokens(o => o.SigningKey = Convert.ToBase64String(new byte[16])))
            .Provider.GetRequiredService<UserTokenOptions>();

        act.Should().Throw<Microsoft.Extensions.Options.OptionsValidationException>().WithMessage("*SigningKey*");
    }
}
