namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Holds the names the API key scheme reads and writes.</summary>
public static class ApiKeyAuthenticationDefaults
{
    /// <summary>Holds the authentication scheme name.</summary>
    public const string Scheme = "ApiKey";

    /// <summary>Holds the header a client may send the secret in instead of a Bearer token.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>Holds the claim that carries the authenticated key's identifier.</summary>
    public const string KeyIdClaim = "api_key_id";

    /// <summary>Holds the claim type that carries each scope the authenticated key grants.</summary>
    public const string ScopeClaim = "scope";
}
