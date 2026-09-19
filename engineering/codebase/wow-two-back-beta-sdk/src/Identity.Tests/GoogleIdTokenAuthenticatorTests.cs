using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;
using WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google.Authenticators;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests;

/// <summary>Covers caller cancellation at the Google ID-token authentication boundary.</summary>
public sealed class GoogleIdTokenAuthenticatorTests
{
    [Fact]
    public async Task AuthenticateAsync_ShouldObservePreCanceledToken()
    {
        var options = new GoogleIdTokenAuthenticatorOptions().WithClientId("client-id");
        var authenticator = new GoogleIdTokenAuthenticator(options, NullLogger<GoogleIdTokenAuthenticator>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = () => authenticator.AuthenticateAsync("token", cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
