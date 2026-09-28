using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Identity.Core;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.Phones;
using WoW.Two.Sdk.Backend.Beta.Identity.Core.SignIn;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Accounts;

/// <summary>Phone slice: confirmation by code, sign-in by code, and silence for unknown numbers.</summary>
public sealed class PhoneSliceTests
{
    private const string Phone = "+998901234567";

    [Fact]
    public async Task ConfirmedNumber_ShouldSignInByCode()
    {
        await using var host = AccountsHost.Create(identity => identity.AddPhoneNumbers().AddSignIn());
        var user = await host.CreateUserAsync("vera");
        await using var scope = host.Scope();
        var phones = scope.ServiceProvider.GetRequiredService<UserPhoneService<IdentityUser, Guid>>();
        var tracked = (await scope.ServiceProvider.GetRequiredService<UserAccountService<IdentityUser, Guid>>().FindByIdAsync(user.Id))!;

        (await phones.SetPhoneNumberAsync(tracked, Phone)).Succeeded.Should().BeTrue();
        (await phones.CreateSignInCodeAsync(Phone)).Should().BeNull();

        var confirmation = await phones.CreateConfirmationCodeAsync(tracked);
        (await phones.ConfirmPhoneNumberAsync(tracked, "000000" == confirmation.Code ? "111111" : "000000")).Errors
            .Should().ContainSingle(e => e.Code == IdentityErrorCodeConstants.InvalidPhoneCode);
        (await phones.ConfirmPhoneNumberAsync(tracked, confirmation.Code!)).Succeeded.Should().BeTrue();
        (await host.ReloadAsync(user.Id)).PhoneNumberConfirmed.Should().BeTrue();

        var signInCode = await phones.CreateSignInCodeAsync(Phone);
        signInCode!.Code.Should().NotBeNullOrEmpty();
        var signedIn = await phones.VerifySignInCodeAsync(Phone, signInCode.Code!);
        signedIn!.Id.Should().Be(user.Id);
        (await scope.ServiceProvider.GetRequiredService<SignInService<IdentityUser, Guid>>().SignInAsync(signedIn)).Status
            .Should().Be(SignInStatus.Succeeded);

        (await phones.CreateSignInCodeAsync("+10000000000")).Should().BeNull();
    }
}
