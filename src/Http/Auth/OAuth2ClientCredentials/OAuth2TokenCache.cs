using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace WoW.Two.Sdk.Backend.Beta.Http.Auth.OAuth2ClientCredentials;

/// <summary>Singleton per-client-name access-token cache that single-flights token-endpoint calls so a cold cache triggers exactly one fetch.</summary>
internal sealed class OAuth2TokenCache : IDisposable
{
    /// <summary>Named <see cref="HttpClient"/> used for token-endpoint calls (no auth handler attached).</summary>
    internal const string TokenHttpClientName = "oauth2-token-endpoint";

    private const double DefaultExpiresInSeconds = 3600;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CachedToken> _tokens = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public OAuth2TokenCache(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetAccessTokenAsync(string clientName, OAuth2ClientCredentialsOptions options, CancellationToken cancellationToken)
    {
        if (_tokens.TryGetValue(clientName, out var cached) && cached.ExpiresAt > _timeProvider.GetUtcNow())
        {
            return cached.AccessToken;
        }

        var gate = _gates.GetOrAdd(clientName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tokens.TryGetValue(clientName, out cached) && cached.ExpiresAt > _timeProvider.GetUtcNow())
            {
                return cached.AccessToken;
            }

            var fresh = await FetchTokenAsync(clientName, options, cancellationToken).ConfigureAwait(false);
            _tokens[clientName] = fresh;
            return fresh.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose()
    {
        foreach (var gate in _gates.Values)
        {
            gate.Dispose();
        }
    }

    private async Task<CachedToken> FetchTokenAsync(string clientName, OAuth2ClientCredentialsOptions options, CancellationToken cancellationToken)
    {
        if (options.TokenEndpoint is null)
        {
            throw new InvalidOperationException($"OAuth2 client credentials for HTTP client '{clientName}': TokenEndpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new InvalidOperationException($"OAuth2 client credentials for HTTP client '{clientName}': ClientId is not configured.");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", options.ClientId),
            new("client_secret", options.ClientSecret),
        };
        if (!string.IsNullOrWhiteSpace(options.Scope))
        {
            form.Add(new("scope", options.Scope));
        }

        foreach (var parameter in options.AdditionalParameters)
        {
            form.Add(new(parameter.Key, parameter.Value));
        }

        using var client = _httpClientFactory.CreateClient(TokenHttpClientName);
        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(options.TokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var tokenElement) ? tokenElement.GetString() : null;
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new InvalidOperationException($"OAuth2 client credentials for HTTP client '{clientName}': token endpoint response has no 'access_token'.");
        }

        var expiresIn = root.TryGetProperty("expires_in", out var expiresElement)
            ? ReadExpiresIn(expiresElement)
            : DefaultExpiresInSeconds;

        var lifetime = TimeSpan.FromSeconds(expiresIn);
        var skew = options.RefreshSkew < lifetime / 2 ? options.RefreshSkew : lifetime / 2;
        return new CachedToken(accessToken, _timeProvider.GetUtcNow() + lifetime - skew);
    }

    private static double ReadExpiresIn(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => DefaultExpiresInSeconds,
    };

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
}
