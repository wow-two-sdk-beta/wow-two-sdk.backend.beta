using System.Net;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Password slice: rules, breach check, set/change/check, stamp rotation, rehash upgrade and token reset.</summary>
public sealed class PasswordSliceTests
{
    [Fact]
    public async Task Rules_ShouldReportEveryFailedRule()
    {
        await using var host = AccountsHost.Create(
            identity => identity.AddArgon2Passwords(),
            options =>
            {
                options.Password.MinLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;
            });
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();

        var result = await passwords.ValidateAsync(new IdentityUser { UserName = "aaaa" }, "aaaa");

        result.Errors.Select(e => e.Code).Should().BeEquivalentTo(
            IdentityErrorCodeConstants.PasswordTooShort,
            IdentityErrorCodeConstants.PasswordRequiresDigit,
            IdentityErrorCodeConstants.PasswordRequiresUpper,
            IdentityErrorCodeConstants.PasswordRequiresNonAlphanumeric,
            IdentityErrorCodeConstants.PasswordRequiresUniqueChars,
            IdentityErrorCodeConstants.PasswordMatchesAccount);
    }

    [Fact]
    public async Task CreateWithPassword_ShouldPersistNothing_WhenThePasswordFails()
    {
        await using var host = AccountsHost.Create(identity => identity.AddArgon2Passwords());
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>();

        var result = await passwords.CreateWithPasswordAsync(new IdentityUser { UserName = "carol", Email = "carol@example.test" }, "short");

        result.Succeeded.Should().BeFalse();
        (await accounts.FindByNameAsync("carol")).Should().BeNull();
    }

    [Fact]
    public async Task ChangePassword_ShouldVerifyTheCurrentPasswordAndRotateTheStamp()
    {
        await using var host = AccountsHost.Create(identity => identity.AddArgon2Passwords());
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
        var user = new IdentityUser { UserName = "dave", Email = "dave@example.test" };
        (await passwords.CreateWithPasswordAsync(user, "correct horse battery")).Succeeded.Should().BeTrue();
        var stamp = user.SecurityStamp;

        (await passwords.ChangePasswordAsync(user, "wrong password", "another long phrase")).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.PasswordMismatch);
        (await passwords.ChangePasswordAsync(user, "correct horse battery", "another long phrase")).Succeeded.Should().BeTrue();

        var stored = await host.ReloadAsync(user.Id);
        stored.SecurityStamp.Should().NotBe(stamp);
        (await passwords.CheckPasswordAsync(stored, "another long phrase")).Should().BeTrue();
        (await passwords.CheckPasswordAsync(stored, "correct horse battery")).Should().BeFalse();
        (await passwords.AddPasswordAsync(stored, "a third long phrase")).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.UserAlreadyHasPassword);
    }

    [Fact]
    public async Task CheckPassword_ShouldUpgradeAHashRecordedWithOlderParameters()
    {
        await using var host = AccountsHost.Create(identity => identity.AddArgon2Passwords());
        var user = await host.CreateUserAsync("erin");
        await using (var scope = host.Scope())
        {
            var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>();
            var tracked = await accounts.FindByIdAsync(user.Id);
            tracked!.PasswordHash = LegacyHash("legacy passphrase");
            await accounts.UpdateAsync(tracked);
        }

        await using (var scope = host.Scope())
        {
            var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
            var stored = await host.ReloadAsync(user.Id);
            (await passwords.CheckPasswordAsync(stored, "legacy passphrase")).Should().BeTrue();
        }

        (await host.ReloadAsync(user.Id)).PasswordHash.Should().StartWith("$argon2id$v=19$m=19456,t=4,p=1$");
    }

    [Fact]
    public async Task ResetPassword_ShouldAcceptOneLiveTokenOnly()
    {
        await using var host = AccountsHost.Create(identity => identity
            .AddArgon2Passwords()
            .AddUserTokens(o => o.SigningKey = AccountsHost.SigningKey));
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
        var user = new IdentityUser { UserName = "frank", Email = "frank@example.test" };
        (await passwords.CreateWithPasswordAsync(user, "original long phrase")).Succeeded.Should().BeTrue();

        var token = passwords.IssuePasswordResetToken(user);
        (await passwords.ResetPasswordAsync(user, token, "replacement long phrase")).Succeeded.Should().BeTrue();
        (await passwords.ResetPasswordAsync(user, token, "a second replacement")).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidToken);

        var expired = passwords.IssuePasswordResetToken(user);
        host.Time.Advance(TimeSpan.FromHours(2) + TimeSpan.FromSeconds(1));
        (await passwords.ResetPasswordAsync(user, expired, "a third replacement")).Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task ResetPassword_ShouldNameTheMissingSlice_WhenTokensAreNotRegistered()
    {
        await using var host = AccountsHost.Create(identity => identity.AddArgon2Passwords());
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();

        var act = () => passwords.IssuePasswordResetToken(new IdentityUser { UserName = "gina" });

        act.Should().Throw<NotSupportedException>().WithMessage("*AddUserTokens*");
    }

    [Fact]
    public async Task BreachCheck_ShouldRejectACorpusHitAndPassWhenTheCorpusIsDown()
    {
        var handler = new RangeHandler("password1");
        await using var host = AccountsHost.Create(
            identity => identity.AddArgon2Passwords().AddBreachedPasswordCheck(),
            services: services => services
                .AddHttpClient<IPwnedPasswordsClient, PwnedPasswordsClient>()
                .ConfigurePrimaryHttpMessageHandler(() => handler));
        await using var scope = host.Scope();
        var passwords = scope.ServiceProvider.GetRequiredService<UserPasswordService<IdentityUser, Guid>>();
        var user = new IdentityUser { UserName = "hank" };

        (await passwords.ValidateAsync(user, "password1")).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.PasswordBreached);
        (await passwords.ValidateAsync(user, "an unbreached phrase")).Succeeded.Should().BeTrue();
        handler.Requests.Should().AllSatisfy(request =>
        {
            request.RequestUri!.AbsolutePath.Should().MatchRegex("^/range/[0-9A-F]{5}$");
            request.Headers.GetValues("Add-Padding").Should().ContainSingle("true");
        });

        handler.Fail = true;
        (await passwords.ValidateAsync(user, "password1")).Succeeded.Should().BeTrue();
    }

    private static string LegacyHash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(password)) { Salt = salt, DegreeOfParallelism = 1, MemorySize = 1024, Iterations = 1 };
        return $"$argon2id$v=19$m=1024,t=1,p=1${Convert.ToBase64String(salt)}${Convert.ToBase64String(argon.GetBytes(32))}";
    }

    /// <summary>Serves a padded range response containing one breached password, or fails on demand.</summary>
    private sealed class RangeHandler(string breached) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public bool Fail { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5350", Justification = "The range API is keyed by SHA-1.")]
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Fail)
                throw new HttpRequestException("corpus unreachable");

            Requests.Add(request);
            var digest = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(breached)));
            var body = request.RequestUri!.AbsolutePath.EndsWith(digest[..5], StringComparison.Ordinal)
                ? $"{new string('0', 35)}:0\r\n{digest[5..]}:42\r\n"
                : $"{new string('F', 35)}:0\r\n";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }
}
