using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Lockout;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Passkeys;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Passkeys: registration, usernameless sign-in, single-use ceremonies and forged or foreign assertions.</summary>
public sealed class PasskeyTests
{
    [Fact]
    public async Task Passkey_ShouldRegisterAndSignInWithoutAUserName()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("pia");
        using var authenticator = new SoftwareAuthenticator();

        await using (var scope = host.Scope())
        {
            var passkeys = scope.ServiceProvider.GetRequiredService<UserPasskeyService<IdentityUser, Guid>>();
            var ceremony = await passkeys.BeginRegistrationAsync(user);
            (await passkeys.CompleteRegistrationAsync(user, ceremony.State, authenticator.Create(ceremony.OptionsJson), "  Pia's laptop  ")).Succeeded.Should().BeTrue();

            var listed = (await passkeys.ListAsync(user)).Should().ContainSingle().Subject;
            listed.Name.Should().Be("Pia's laptop");
            listed.CreatedAt.Should().Be(host.Time.GetUtcNow());
            listed.LastUsedAt.Should().BeNull();

            var again = await passkeys.BeginRegistrationAsync(user);
            again.OptionsJson.Should().Contain(Convert.ToBase64String(authenticator.CredentialId).TrimEnd('=').Replace('+', '-').Replace('/', '_'), "registered passkeys are excluded");
        }

        host.Time.Advance(TimeSpan.FromMinutes(1));
        await using (var scope = host.Scope())
        {
            var passkeys = scope.ServiceProvider.GetRequiredService<UserPasskeyService<IdentityUser, Guid>>();
            var signIn = scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>();
            var ceremony = passkeys.BeginSignIn();
            ceremony.OptionsJson.Should().NotContain("allowCredentials\":[{", "a usernameless request names no credentials");

            var result = await signIn.PasskeySignInAsync(ceremony.State, authenticator.Get(ceremony.OptionsJson));
            result.Succeeded.Should().BeTrue();
            result.UserId.Should().Be(user.Id.ToString());
            result.Principal.Should().NotBeNull();
            (await passkeys.ListAsync(user)).Single().LastUsedAt.Should().Be(host.Time.GetUtcNow());
        }
    }

    [Fact]
    public async Task SignInCeremony_ShouldCompleteOnce_EvenWithoutASignatureCounter()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("rex");
        using var synced = new SoftwareAuthenticator(countsSignatures: false);
        await RegisterAsync(host, user, synced);

        await using var scope = host.Scope();
        var passkeys = scope.ServiceProvider.GetRequiredService<UserPasskeyService<IdentityUser, Guid>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>();

        var stale = passkeys.BeginSignIn();
        host.Time.Advance(TimeSpan.FromSeconds(5));
        var ceremony = passkeys.BeginSignIn();
        var assertion = synced.Get(ceremony.OptionsJson);
        (await signIn.PasskeySignInAsync(ceremony.State, assertion)).Succeeded.Should().BeTrue();
        synced.SignCount.Should().Be(0);

        (await signIn.PasskeySignInAsync(ceremony.State, assertion)).Status.Should().Be(SignInStatus.Failed, "the same ceremony replayed");
        (await signIn.PasskeySignInAsync(stale.State, synced.Get(stale.OptionsJson))).Status.Should().Be(SignInStatus.Failed, "a ceremony older than the last use");

        host.Time.Advance(TimeSpan.FromSeconds(5));
        var next = passkeys.BeginSignIn();
        (await signIn.PasskeySignInAsync(next.State, synced.Get(next.OptionsJson))).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Ceremonies_ShouldExpireAndStayWithTheirUserAndKind()
    {
        await using var host = CreateHost();
        var owner = await host.CreateUserAsync("ola");
        var other = await host.CreateUserAsync("ivo");
        using var authenticator = new SoftwareAuthenticator();

        await using var scope = host.Scope();
        var passkeys = scope.ServiceProvider.GetRequiredService<UserPasskeyService<IdentityUser, Guid>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>();

        var ceremony = await passkeys.BeginRegistrationAsync(owner);
        var credential = authenticator.Create(ceremony.OptionsJson);
        (await passkeys.CompleteRegistrationAsync(other, ceremony.State, credential)).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidPasskey);
        (await passkeys.CompleteRegistrationAsync(owner, ceremony.State[..^4] + "AAAA", credential)).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidPasskey);
        (await passkeys.CompleteRegistrationAsync(owner, ceremony.State, "{not json")).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidPasskey);

        host.Time.Advance(TimeSpan.FromMinutes(5));
        (await passkeys.CompleteRegistrationAsync(owner, ceremony.State, credential)).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidPasskey, "the ceremony expired");

        var fresh = await passkeys.BeginRegistrationAsync(owner);
        (await passkeys.CompleteRegistrationAsync(owner, fresh.State, authenticator.Create(fresh.OptionsJson))).Succeeded.Should().BeTrue();
        var repeat = await passkeys.BeginRegistrationAsync(other);
        (await passkeys.CompleteRegistrationAsync(other, repeat.State, authenticator.Create(repeat.OptionsJson))).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidPasskey, "a credential registers once");

        var registration = await passkeys.BeginRegistrationAsync(owner);
        (await signIn.PasskeySignInAsync(registration.State, authenticator.Get(passkeys.BeginSignIn().OptionsJson))).Status.Should().Be(SignInStatus.Failed, "a registration state cannot sign in");
    }

    [Fact]
    public async Task ForgedOrForeignAssertions_ShouldFail()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("eve");
        using var authenticator = new SoftwareAuthenticator();
        using var forger = new SoftwareAuthenticator(credentialId: authenticator.CredentialId);
        await RegisterAsync(host, user, authenticator);

        await using var scope = host.Scope();
        var passkeys = scope.ServiceProvider.GetRequiredService<UserPasskeyService<IdentityUser, Guid>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>();

        var ceremony = passkeys.BeginSignIn();
        forger.Create((await passkeys.BeginRegistrationAsync(user)).OptionsJson);
        (await signIn.PasskeySignInAsync(ceremony.State, forger.Get(ceremony.OptionsJson))).Status.Should().Be(SignInStatus.Failed, "another key signed it");

        authenticator.Origin = "https://evil.test";
        (await signIn.PasskeySignInAsync(ceremony.State, authenticator.Get(ceremony.OptionsJson))).Status.Should().Be(SignInStatus.Failed, "a phishing origin");

        using var unknown = new SoftwareAuthenticator();
        unknown.Create((await passkeys.BeginRegistrationAsync(user)).OptionsJson);
        (await signIn.PasskeySignInAsync(ceremony.State, unknown.Get(ceremony.OptionsJson))).Status.Should().Be(SignInStatus.Failed, "an unregistered credential");

        authenticator.Origin = "https://app.test";
        (await signIn.PasskeySignInAsync(ceremony.State, authenticator.Get(ceremony.OptionsJson))).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task SignIn_ShouldHonourLockoutAndRemoval()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("lou");
        var bystander = await host.CreateUserAsync("bea");
        using var authenticator = new SoftwareAuthenticator();
        await RegisterAsync(host, user, authenticator);

        await using var scope = host.Scope();
        var services = scope.ServiceProvider;
        var passkeys = services.GetRequiredService<UserPasskeyService<IdentityUser, Guid>>();
        var signIn = services.GetRequiredService<SignInService<IdentityUser, Guid>>();
        var current = await services.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(user.Id);

        (await services.GetRequiredService<UserLockoutService<IdentityUser, Guid>>().LockUntilAsync(current!, host.Time.GetUtcNow().AddHours(1))).Succeeded.Should().BeTrue();
        var ceremony = passkeys.BeginSignIn();
        (await signIn.PasskeySignInAsync(ceremony.State, authenticator.Get(ceremony.OptionsJson))).Status.Should().Be(SignInStatus.LockedOut);

        var id = (await passkeys.ListAsync(current!)).Single().Id;
        (await passkeys.RemoveAsync(bystander, id)).Should().BeFalse("only the owner removes a passkey");
        (await passkeys.RemoveAsync(current!, id)).Should().BeTrue();
        (await passkeys.ListAsync(current!)).Should().BeEmpty();
    }

    [Fact]
    public void Options_ShouldRequireTheRelyingParty()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddUserAccounts<IdentityUser>().AddPasskeys();
        using var provider = services.BuildServiceProvider();

        var failure = FluentActions.Invoking(() => provider.GetRequiredService<PasskeyOptions>()).Should().Throw<OptionsValidationException>().Which;
        failure.Failures.Should().Contain("PasskeyOptions.ServerDomain is required.").And.Contain("PasskeyOptions.Origins needs at least one origin.");
    }

    private static AccountsHost CreateHost() => AccountsHost.Create(
        identity => identity
            .AddLockout()
            .AddPasskeys(o =>
            {
                o.ServerDomain = "app.test";
                o.ServerName = "Tests";
                o.Origins.Add("https://app.test");
            })
            .AddSignIn(),
        services: services => services.AddDataProtection().UseEphemeralDataProtectionProvider());

    private static async Task RegisterAsync(AccountsHost host, IdentityUser user, SoftwareAuthenticator authenticator)
    {
        await using var scope = host.Scope();
        var passkeys = scope.ServiceProvider.GetRequiredService<UserPasskeyService<IdentityUser, Guid>>();
        var ceremony = await passkeys.BeginRegistrationAsync(user);
        (await passkeys.CompleteRegistrationAsync(user, ceremony.State, authenticator.Create(ceremony.OptionsJson))).Succeeded.Should().BeTrue();
        host.Time.Advance(TimeSpan.FromSeconds(1));
    }
}
