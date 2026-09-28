using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Lockout;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Logins;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Roles;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.UserClaims;
using WoW.Two.Sdk.Backend.Beta.Identity.Mfa.Totp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Roles, user claims, the principal factory, sign-in orchestration, two-factor and external logins.</summary>
public sealed class RolesAndSignInTests
{
    private const string Password = "correct horse battery";

    private static AccountsHost CreateHost(Action<IdentityCoreOptions>? options = null) => AccountsHost.Create(
        identity => identity
            .AddArgon2Passwords()
            .AddUserTokens(o => o.SigningKey = AccountsHost.SigningKey)
            .AddLockout()
            .AddRoles<IdentityRole>()
            .AddUserClaims()
            .AddExternalLogins()
            .AddTwoFactor(o => o.Issuer = "Tests")
            .AddSignIn(),
        options);

    [Fact]
    public async Task Principal_ShouldCarryIdentityRolesRoleClaimsAndUserClaims()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("nora");
        await using var scope = host.Scope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleService<IdentityRole, Guid>>();
        var memberships = scope.ServiceProvider.GetRequiredService<UserRoleService<IdentityUser, IdentityRole, Guid>>();
        var claims = scope.ServiceProvider.GetRequiredService<UserClaimService<IdentityUser, Guid>>();

        var admin = new IdentityRole { Name = "Admin" };
        (await roles.CreateAsync(admin)).Succeeded.Should().BeTrue();
        (await roles.CreateAsync(new IdentityRole { Name = "ADMIN" })).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.DuplicateRoleName);
        await roles.AddClaimAsync(admin, new Claim("permission", "users.write"));
        (await memberships.AddToRoleAsync(user, "admin")).Succeeded.Should().BeTrue();
        (await memberships.AddToRoleAsync(user, "Admin")).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.UserAlreadyInRole);
        (await memberships.AddToRoleAsync(user, "Missing")).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.RoleNotFound);
        await claims.AddClaimsAsync(user, [new Claim("tier", "gold")]);

        var principal = await scope.ServiceProvider.GetRequiredService<UserClaimsPrincipalFactory<IdentityUser, Guid>>().CreateAsync(user);

        principal.Identity!.IsAuthenticated.Should().BeTrue();
        principal.Identity.Name.Should().Be("nora");
        principal.IsInRole("Admin").Should().BeTrue();
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.Id.ToString());
        principal.FindFirst("AspNet.Identity.SecurityStamp")!.Value.Should().Be(user.SecurityStamp);
        principal.HasClaim("permission", "users.write").Should().BeTrue();
        principal.HasClaim("tier", "gold").Should().BeTrue();
        (await memberships.GetUserIdsInRoleAsync("admin")).Should().ContainSingle().Which.Should().Be(user.Id);
    }

    [Fact]
    public async Task RoleRemovalAndClaimRemoval_ShouldRotateTheStamp()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("owen");
        await using var scope = host.Scope();
        var tracked = (await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(user.Id))!;
        await scope.ServiceProvider.GetRequiredService<RoleService<IdentityRole, Guid>>().CreateAsync(new IdentityRole { Name = "Editor" });
        var memberships = scope.ServiceProvider.GetRequiredService<UserRoleService<IdentityUser, IdentityRole, Guid>>();
        var claims = scope.ServiceProvider.GetRequiredService<UserClaimService<IdentityUser, Guid>>();
        await memberships.AddToRoleAsync(tracked, "Editor");
        await claims.AddClaimsAsync(tracked, [new Claim("beta", "yes")]);

        var stamp = tracked.SecurityStamp;
        (await memberships.RemoveFromRoleAsync(tracked, "Editor")).Succeeded.Should().BeTrue();
        tracked.SecurityStamp.Should().NotBe(stamp);
        (await memberships.GetRolesAsync(tracked)).Should().BeEmpty();

        stamp = tracked.SecurityStamp;
        (await claims.RemoveClaimAsync(tracked, new Claim("beta", "yes"))).Succeeded.Should().BeTrue();
        tracked.SecurityStamp.Should().NotBe(stamp);
        (await claims.GetClaimsAsync(tracked)).Should().BeEmpty();
    }

    [Fact]
    public async Task PasswordSignIn_ShouldSucceedByNameOrEmailAndLockAfterRepeatedFailures()
    {
        await using var host = CreateHost(o => o.Lockout.MaxFailedAttempts = 2);
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>();
        (await passwords.CreateWithPasswordAsync(new IdentityUser { UserName = "pete", Email = "pete@example.test" }, Password)).Succeeded.Should().BeTrue();

        var byName = await signIn.PasswordSignInAsync("PETE", Password);
        byName.Succeeded.Should().BeTrue();
        byName.Principal!.Identity!.Name.Should().Be("pete");
        (await signIn.PasswordSignInAsync("pete@example.test", Password)).Succeeded.Should().BeTrue();
        (await signIn.PasswordSignInAsync("nobody", Password)).Status.Should().Be(SignInStatus.Failed);

        (await signIn.PasswordSignInAsync("pete", "wrong")).Status.Should().Be(SignInStatus.Failed);
        (await signIn.PasswordSignInAsync("pete", "wrong")).Status.Should().Be(SignInStatus.LockedOut);
        (await signIn.PasswordSignInAsync("pete", Password)).Status.Should().Be(SignInStatus.LockedOut);

        host.Time.Advance(TimeSpan.FromMinutes(5));
        (await signIn.PasswordSignInAsync("pete", Password)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task PasswordSignIn_ShouldRefuseAnUnconfirmedEmail_WhenConfirmationIsRequired()
    {
        await using var host = CreateHost(o => o.SignIn.RequireConfirmedEmail = true);
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
        var user = new IdentityUser { UserName = "quinn", Email = "quinn@example.test" };
        await passwords.CreateWithPasswordAsync(user, Password);

        var result = await scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>().PasswordSignInAsync("quinn", Password);

        result.Status.Should().Be(SignInStatus.NotAllowed);
        result.Principal.Should().BeNull();
    }

    [Fact]
    public async Task TwoFactor_ShouldGateSignInBehindATicketAndAnAuthenticatorOrRecoveryCode()
    {
        await using var host = CreateHost();
        await using var scope = host.Scope();
        var services = scope.ServiceProvider;
        var passwords = services.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
        var twoFactor = services.GetRequiredService<UserTwoFactorService<IdentityUser, Guid>>();
        var signIn = services.GetRequiredService<SignInService<IdentityUser, Guid>>();
        var totp = services.GetRequiredService<ITotpService>();
        var user = new IdentityUser { UserName = "rita", Email = "rita@example.test" };
        await passwords.CreateWithPasswordAsync(user, Password);

        (await twoFactor.EnableAsync(user, "000000")).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.AuthenticatorNotConfigured);
        var key = await twoFactor.ResetAuthenticatorKeyAsync(user);
        (await twoFactor.GetAuthenticatorUriAsync(user))!.ToString().Should().StartWith("otpauth://totp/").And.Contain("Tests");
        (await twoFactor.EnableAsync(user, totp.ComputeCode(Base32Encoding.ToBytes(key)))).Succeeded.Should().BeTrue();
        var recovery = await twoFactor.GenerateRecoveryCodesAsync(user);
        recovery.Should().HaveCount(10).And.OnlyContain(code => code.Length == 11 && code[5] == '-');

        var first = await signIn.PasswordSignInAsync("rita", Password);
        first.Status.Should().Be(SignInStatus.RequiresTwoFactor);
        first.Principal.Should().BeNull();
        first.TwoFactorTicket.Should().NotBeNullOrEmpty();

        (await signIn.TwoFactorSignInAsync(user.Id, "forged-ticket", totp.ComputeCode(Base32Encoding.ToBytes(key)))).Status.Should().Be(SignInStatus.Failed);
        (await signIn.TwoFactorSignInAsync(user.Id, first.TwoFactorTicket!, "000000")).Status.Should().Be(SignInStatus.Failed);
        (await signIn.TwoFactorSignInAsync(user.Id, first.TwoFactorTicket!, totp.ComputeCode(Base32Encoding.ToBytes(key)))).Succeeded.Should().BeTrue();

        var second = await signIn.PasswordSignInAsync("rita", Password);
        (await signIn.RecoveryCodeSignInAsync(user.Id, second.TwoFactorTicket!, recovery[0].ToLowerInvariant())).Succeeded.Should().BeTrue();
        (await signIn.RecoveryCodeSignInAsync(user.Id, second.TwoFactorTicket!, recovery[0])).Status.Should().Be(SignInStatus.Failed);
        (await twoFactor.CountRecoveryCodesAsync(user)).Should().Be(9);

        host.Time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        (await signIn.RecoveryCodeSignInAsync(user.Id, second.TwoFactorTicket!, recovery[1])).Status.Should().Be(SignInStatus.Failed);
    }

    [Fact]
    public async Task ExternalLogins_ShouldLinkFindAndUnlinkAProviderAccount()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("sam");
        var other = await host.CreateUserAsync("tess");
        await using var scope = host.Scope();
        var logins = scope.ServiceProvider.GetRequiredService<UserLoginService<IdentityUser, Guid>>();
        var signIn = scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>();

        (await logins.AddLoginAsync(user, "Google", "google-sub-1", "Google")).Succeeded.Should().BeTrue();
        (await logins.AddLoginAsync(other, "Google", "google-sub-1")).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.LoginAlreadyAssociated);

        var found = await logins.FindByLoginAsync("Google", "google-sub-1");
        found!.Id.Should().Be(user.Id);
        (await signIn.SignInAsync(found)).Succeeded.Should().BeTrue();

        (await logins.RemoveLoginAsync(found, "Google", "google-sub-1")).Succeeded.Should().BeTrue();
        (await logins.FindByLoginAsync("Google", "google-sub-1")).Should().BeNull();
    }

    [Fact]
    public async Task DeletingAUser_ShouldRemoveItsRelationRows()
    {
        await using var host = CreateHost();
        var user = await host.CreateUserAsync("uma");
        await using var scope = host.Scope();
        var services = scope.ServiceProvider;
        var accounts = services.GetRequiredService<UserAccountService<IdentityUser, Guid>>();
        await services.GetRequiredService<RoleService<IdentityRole, Guid>>().CreateAsync(new IdentityRole { Name = "Viewer" });
        var tracked = (await accounts.FindByIdAsync(user.Id))!;
        await services.GetRequiredService<UserRoleService<IdentityUser, IdentityRole, Guid>>().AddToRoleAsync(tracked, "Viewer");
        await services.GetRequiredService<UserClaimService<IdentityUser, Guid>>().AddClaimsAsync(tracked, [new Claim("a", "b")]);
        await services.GetRequiredService<UserLoginService<IdentityUser, Guid>>().AddLoginAsync(tracked, "GitHub", "gh-1");
        await services.GetRequiredService<UserTwoFactorService<IdentityUser, Guid>>().ResetAuthenticatorKeyAsync(tracked);

        (await accounts.DeleteAsync(tracked)).Succeeded.Should().BeTrue();

        var context = services.GetRequiredService<AccountsDbContext>();
        context.Set<IdentityUserRole<Guid>>().Should().BeEmpty();
        context.Set<IdentityUserClaim<Guid>>().Should().BeEmpty();
        context.Set<IdentityUserLogin<Guid>>().Should().BeEmpty();
        context.Set<IdentityUserToken<Guid>>().Should().BeEmpty();
    }
}
