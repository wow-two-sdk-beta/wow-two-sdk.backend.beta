namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google.Authenticators;

/// <summary>Defines authentication from a Google ID token (the SPA/client-side sign-in flow) and returns its trusted identity, or <c>null</c> when the token is invalid; the seam that keeps sign-in testable without calling Google.</summary>
public interface IGoogleIdTokenAuthenticator
{
    /// <summary>Validates the signature, audience, and expiry of <paramref name="idToken"/>; returns the verified identity, or <c>null</c> when it cannot be trusted.</summary>
    /// <param name="idToken">The raw Google ID token (JWT) presented by the client.</param>
    /// <param name="cancellationToken">Token that stops awaiting provider validation; an in-flight provider certificate refresh cannot be interrupted.</param>
    Task<GoogleVerifiedIdentity?> AuthenticateAsync(string idToken, CancellationToken cancellationToken = default);
}
