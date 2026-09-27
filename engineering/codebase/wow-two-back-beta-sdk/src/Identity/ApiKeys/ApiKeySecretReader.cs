using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

/// <summary>Reads the API key secret a request presents — a marked Bearer token, or the key header.</summary>
/// <remarks>
/// A Bearer token without the product's marker is not an API key — a JWT, say — so it is left to other schemes.
/// </remarks>
/// <param name="options">The key shape naming the marker and the header.</param>
/// <param name="secrets">The secret factory that knows the marker.</param>
public sealed class ApiKeySecretReader(IOptions<ApiKeyOptions> options, ApiKeySecretFactory secrets)
{
    /// <summary>Holds the authorization scheme prefix a Bearer token starts with.</summary>
    private const string BearerPrefix = "Bearer ";

    /// <summary>Reads the presented secret.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The secret, or <c>null</c> when the request presents no API key.</returns>
    public string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[BearerPrefix.Length..].Trim();
            if (secrets.HasMarker(token))
                return token;
        }

        var header = request.Headers[options.Value.HeaderName].ToString().Trim();
        return header.Length > 0 ? header : null;
    }
}
