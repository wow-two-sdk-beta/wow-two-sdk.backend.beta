namespace WoW.Two.Sdk.Backend.Beta.http.auth_oauth2_client_credentials;

/// <summary>
/// Settings for the OAuth2 <c>client_credentials</c> bearer-token handler attached to an outbound
/// <see cref="HttpClient"/>. Tokens are fetched from <see cref="TokenEndpoint"/>, cached per client
/// name, and refreshed <see cref="RefreshSkew"/> before expiry.
/// </summary>
public sealed class OAuth2ClientCredentialsOptions
{
    /// <summary>Absolute URL of the OAuth2 token endpoint. Required.</summary>
    public Uri? TokenEndpoint { get; set; }

    /// <summary>OAuth2 client identifier. Required.</summary>
    public string ClientId { get; set; } = "";

    /// <summary>OAuth2 client secret. Required.</summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>Optional space-separated scope value sent with the token request.</summary>
    public string? Scope { get; set; }

    /// <summary>Extra form fields for the token request (e.g. <c>audience</c> for Auth0-style servers).</summary>
    public IDictionary<string, string> AdditionalParameters { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>How long before the token's expiry a fresh one is fetched. Default 60s.</summary>
    public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromSeconds(60);
}
