using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Lockout;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Passwords;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Tokens;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.TwoFactor;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Delivered-code second factors: availability by confirmed address, send, verify, enable, preference and ticketed sign-in.</summary>
public sealed class TwoFactorMethodTests
{
    private const string Password = "correct horse battery";
    private const string Phone = "+998901234567";

    [Fact]
    public async Task SmsMethod_ShouldEnableAndThenCompleteASignIn()
    {
        var channels = new CapturingChannels();
        await using var host = CreateHost(channels);
        await using var scope = host.Scope();
        var services = scope.ServiceProvider;
        var methods = services.GetRequiredService<UserTwoFactorMethodService<IdentityUser, Guid>>();
        var signIn = services.GetRequiredService<SignInService<IdentityUser, Guid>>();
        var user = await CreateUserAsync(services, "sami", phoneConfirmed: true);

        (await methods.GetAvailableMethodsAsync(user)).Should().Equal("sms");
        (await methods.SendCodeAsync(user, "email")).Status.Should().Be(TwoFactorCodeStatus.Unavailable);
        (await methods.SendCodeAsync(user, "authenticator")).Status.Should().Be(TwoFactorCodeStatus.NotDeliverable);

        var sent = await methods.SendCodeAsync(user, "sms");
        sent.Status.Should().Be(TwoFactorCodeStatus.Sent);
        sent.ExpiresAt.Should().Be(host.Time.GetUtcNow().AddMinutes(5));
        var envelope = channels.Last("sms");
        envelope.DeliveryAddress.Should().Be(Phone);
        envelope.Code.Should().MatchRegex("^[0-9]{6}$");
        envelope.Text.Should().Be($"{envelope.Code} is your Tests verification code. It expires in 5 minutes. Do not share it with anyone.");
        (await methods.SendCodeAsync(user, "sms")).Status.Should().Be(TwoFactorCodeStatus.RateLimited);

        (await methods.EnableAsync(user, "sms", Wrong(envelope.Code))).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidTwoFactorCode);
        (await methods.EnableAsync(user, "sms", envelope.Code)).Succeeded.Should().BeTrue();
        user.TwoFactorEnabled.Should().BeTrue();
        (await methods.GetPreferredMethodAsync(user)).Should().Be("sms");

        var challenge = await signIn.PasswordSignInAsync("sami", Password);
        challenge.Status.Should().Be(SignInStatus.RequiresTwoFactor);
        challenge.TwoFactorMethod.Should().Be("sms");
        (await signIn.SendTwoFactorCodeAsync(user.Id, "forged-ticket")).Status.Should().Be(TwoFactorCodeStatus.Unavailable);
        (await signIn.SendTwoFactorCodeAsync(user.Id, challenge.TwoFactorTicket!)).Status.Should().Be(TwoFactorCodeStatus.Sent);

        var code = channels.Last("sms").Code;
        (await signIn.TwoFactorSignInAsync(user.Id, challenge.TwoFactorTicket!, "sms", Wrong(code))).Status.Should().Be(SignInStatus.Failed);
        (await signIn.TwoFactorSignInAsync(user.Id, challenge.TwoFactorTicket!, "sms", code)).Succeeded.Should().BeTrue();
        (await signIn.TwoFactorSignInAsync(user.Id, challenge.TwoFactorTicket!, "sms", code)).Status.Should().Be(SignInStatus.Failed);
    }

    [Fact]
    public async Task EmailMethod_ShouldUseItsOwnCodeShapeAndBecomePreferred()
    {
        var channels = new CapturingChannels();
        await using var host = CreateHost(channels);
        await using var scope = host.Scope();
        var services = scope.ServiceProvider;
        var methods = services.GetRequiredService<UserTwoFactorMethodService<IdentityUser, Guid>>();
        var user = await CreateUserAsync(services, "emil", phoneConfirmed: true, emailConfirmed: true);

        (await methods.GetAvailableMethodsAsync(user)).Should().BeEquivalentTo("sms", "email");
        (await methods.SendCodeAsync(user, "EMAIL")).Status.Should().Be(TwoFactorCodeStatus.Sent);

        var envelope = channels.Last("email");
        envelope.DeliveryAddress.Should().Be("emil@example.test");
        envelope.Code.Should().MatchRegex("^[A-HJ-NP-Z2-9]{8}$");
        envelope.Subject.Should().Be("Your Tests verification code");
        envelope.Lifetime.Should().Be(TimeSpan.FromMinutes(10));

        var typed = $"{envelope.Code[..4].ToLowerInvariant()}-{envelope.Code[4..]}";
        (await methods.EnableAsync(user, "email", typed)).Succeeded.Should().BeTrue();
        (await methods.GetPreferredMethodAsync(user)).Should().Be("email");

        (await methods.SetPreferredMethodAsync(user, "whatsapp")).Errors.Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.TwoFactorMethodUnavailable);
        (await methods.SetPreferredMethodAsync(user, "sms")).Succeeded.Should().BeTrue();
        (await methods.GetPreferredMethodAsync(user)).Should().Be("sms");

        await services.GetRequiredService<UserTwoFactorService<IdentityUser, Guid>>().DisableAsync(user);
        user.TwoFactorEnabled.Should().BeFalse();
        (await methods.GetPreferredMethodAsync(user)).Should().Be("sms", "the stored choice is gone, so the first available method leads");
    }

    [Fact]
    public async Task Methods_WithoutARegisteredChannelOrAddress_AreNotAvailable()
    {
        await using var host = AccountsHost.Create(identity => identity
            .AddArgon2Passwords()
            .AddUserTokens(o => o.SigningKey = AccountsHost.SigningKey)
            .AddTwoFactor(o => o.Methods["whatsapp"] = new TwoFactorMethodOptions { Channel = OtpChannelNameConstants.WhatsApp })
            .AddSignIn());
        await using var scope = host.Scope();
        var methods = scope.ServiceProvider.GetRequiredService<UserTwoFactorMethodService<IdentityUser, Guid>>();
        var user = await CreateUserAsync(scope.ServiceProvider, "wale", phoneConfirmed: true);

        (await methods.GetAvailableMethodsAsync(user)).Should().BeEmpty();
        (await methods.SendCodeAsync(user, "whatsapp")).Status.Should().Be(TwoFactorCodeStatus.Unavailable);
        (await methods.GetPreferredMethodAsync(user)).Should().BeNull();
    }

    private static AccountsHost CreateHost(CapturingChannels channels) => AccountsHost.Create(
        identity => identity
            .AddArgon2Passwords()
            .AddUserTokens(o => o.SigningKey = AccountsHost.SigningKey)
            .AddLockout()
            .AddTwoFactor(o =>
            {
                o.Issuer = "Tests";
                o.Methods["sms"] = new TwoFactorMethodOptions { Channel = OtpChannelNameConstants.Sms };
                o.Methods["email"] = new TwoFactorMethodOptions
                {
                    Channel = OtpChannelNameConstants.Email,
                    Code = { Kind = OtpCodeKind.Alphanumeric, Length = 8, Lifetime = TimeSpan.FromMinutes(10) },
                };
            })
            .AddSignIn(),
        services: services =>
        {
            services.AddOtpService(o => o.Messages.AppName = "Tests");
            services.AddKeyedSingleton<IOtpDeliveryHandler>(OtpChannelNameConstants.Sms, channels.For("sms"));
            services.AddKeyedSingleton<IOtpDeliveryHandler>(OtpChannelNameConstants.Email, channels.For("email"));
        });

    private static async Task<IdentityUser> CreateUserAsync(IServiceProvider services, string name, bool phoneConfirmed = false, bool emailConfirmed = false)
    {
        var user = new IdentityUser { UserName = name, Email = $"{name}@example.test", PhoneNumber = Phone, PhoneNumberConfirmed = phoneConfirmed, EmailConfirmed = emailConfirmed };
        (await services.GetRequiredService<UserPasswordService<IdentityUser, Guid>>().CreateWithPasswordAsync(user, Password)).Succeeded.Should().BeTrue();
        return user;
    }

    private static string Wrong(string code) => code == "000000" ? "111111" : "000000";

    /// <summary>Records every delivery per channel and accepts it.</summary>
    private sealed class CapturingChannels
    {
        private readonly List<(string Channel, OtpDeliveryEnvelopeModel Envelope)> _sent = [];

        public IOtpDeliveryHandler For(string channel) => new Handler(this, channel);

        public OtpDeliveryEnvelopeModel Last(string channel) => _sent.Last(entry => entry.Channel == channel).Envelope;

        private sealed class Handler(CapturingChannels owner, string channel) : IOtpDeliveryHandler
        {
            public Task<OtpDeliveryResult> SendAsync(OtpDeliveryEnvelopeModel envelope, CancellationToken cancellationToken = default)
            {
                owner._sent.Add((channel, envelope));
                return Task.FromResult(new OtpDeliveryResult { Success = true });
            }
        }
    }
}
