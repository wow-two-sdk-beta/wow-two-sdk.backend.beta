namespace WoW.Two.Sdk.Backend.Beta.Identity.OAuth.Google;

/// <summary>Configuration for Google ID-token verification; the audience MUST be the OAuth client id the SPA obtained its token for.</summary>
public sealed class GoogleIdTokenVerifierOptions
{
    /// <summary>Accepted audience(s) — the OAuth client id(s) tokens must be issued for. A token whose <c>aud</c> matches none is rejected.</summary>
    public IList<string> Audiences { get; } = [];

    /// <summary>Adds one accepted audience (OAuth client id) — convenience for the single-client case.</summary>
    /// <param name="clientId">The OAuth client id to accept as the token audience.</param>
    public GoogleIdTokenVerifierOptions WithClientId(string clientId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        Audiences.Add(clientId);
        return this;
    }
}
