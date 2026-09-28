using System.Buffers.Text;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.Fcm;

/// <summary>
/// Integrates Firebase Cloud Messaging HTTP v1. Exchanges an RS256 service-account assertion for an OAuth access token,
/// reuses it until five minutes before expiry, and marks <c>UNREGISTERED</c> or invalid tokens.
/// </summary>
public sealed class FcmPushBroker : IPushBroker, IDisposable
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.push.fcm";

    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ServiceAccount _account;
    private readonly RSA _key;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private (string Token, DateTimeOffset ExpiresAt)? _accessToken;

    /// <summary>Create the broker over the service account.</summary>
    /// <param name="httpClientFactory">Creates the named client.</param>
    /// <param name="options">The service-account JSON.</param>
    /// <param name="timeProvider">The clock assertions and token expiry use.</param>
    public FcmPushBroker(IHttpClientFactory httpClientFactory, FcmPushOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _httpClientFactory = httpClientFactory;
        _timeProvider = timeProvider;
        _account = JsonSerializer.Deserialize<ServiceAccount>(options.ServiceAccountJson)
            ?? throw new InvalidOperationException("FcmPushOptions.ServiceAccountJson is empty.");
        _key = RSA.Create();
        _key.ImportFromPem(_account.PrivateKey);
    }

    /// <inheritdoc />
    public async Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var http = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"v1/projects/{Uri.EscapeDataString(_account.ProjectId)}/messages:send")
            {
                Content = new StringContent(new JsonObject { ["message"] = Payload(message) }.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(http, cancellationToken));

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return new PushSendResult { Success = true, ProviderMessageId = (await response.Content.ReadFromJsonAsync<FcmSent>(cancellationToken))?.Name };

            var error = (await response.Content.ReadFromJsonAsync<FcmFailure>(cancellationToken))?.Error;
            var errorCode = error?.Details?.Select(detail => detail.ErrorCode).FirstOrDefault(code => code is not null);
            return new PushSendResult
            {
                Success = false,
                FailureReason = $"fcm_{errorCode ?? error?.Status ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture)}: {error?.Message}",
                TokenInvalid = errorCode is "UNREGISTERED" || (errorCode is "INVALID_ARGUMENT" && error?.Message?.Contains("registration token", StringComparison.OrdinalIgnoreCase) == true),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new PushSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _key.Dispose();
        _tokenGate.Dispose();
    }

    private static JsonObject Payload(PushMessage message)
    {
        var payload = new JsonObject { ["token"] = message.DeviceToken };
        if (message.Title is not null || message.Body is not null)
            payload["notification"] = new JsonObject { ["title"] = message.Title, ["body"] = message.Body };
        if (message.Data is { Count: > 0 } data)
            payload["data"] = new JsonObject(data.Select(pair => KeyValuePair.Create(pair.Key, (JsonNode?)pair.Value)));

        var android = new JsonObject();
        if (message.CollapseKey is not null)
            android["collapse_key"] = message.CollapseKey;
        if (message.TimeToLive is { } ttl)
            android["ttl"] = $"{(long)ttl.TotalSeconds}s";
        if (message.Sound is not null)
            android["notification"] = new JsonObject { ["sound"] = message.Sound };
        if (android.Count > 0)
            payload["android"] = android;

        var aps = new JsonObject();
        if (message.Badge is { } badge)
            aps["badge"] = badge;
        if (message.Sound is not null)
            aps["sound"] = message.Sound;
        if (aps.Count > 0)
            payload["apns"] = new JsonObject { ["payload"] = new JsonObject { ["aps"] = aps } };

        return payload;
    }

    private async Task<string> AccessTokenAsync(HttpClient http, CancellationToken cancellationToken)
    {
        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            if (_accessToken is { } cached && cached.ExpiresAt - TimeSpan.FromMinutes(5) > now)
                return cached.Token;

            using var content = new FormUrlEncodedContent(
            [
                new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"),
                new("assertion", Assertion(now)),
            ]);
            using var response = await http.PostAsync(new Uri(_account.TokenUri), content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var granted = await response.Content.ReadFromJsonAsync<OAuthToken>(cancellationToken)
                ?? throw new InvalidOperationException("The token endpoint returned no access token.");
            _accessToken = (granted.AccessToken, now + TimeSpan.FromSeconds(granted.ExpiresIn));
            return granted.AccessToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private string Assertion(DateTimeOffset now)
    {
        var header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["alg"] = "RS256", ["typ"] = "JWT" }));
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["iss"] = _account.ClientEmail,
            ["scope"] = Scope,
            ["aud"] = _account.TokenUri,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddHours(1).ToUnixTimeSeconds(),
        }));
        var signingInput = $"{header}.{claims}";
        var signature = _key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    private sealed record ServiceAccount
    {
        [JsonPropertyName("project_id")]
        public required string ProjectId { get; init; }

        [JsonPropertyName("client_email")]
        public required string ClientEmail { get; init; }

        [JsonPropertyName("private_key")]
        public required string PrivateKey { get; init; }

        [JsonPropertyName("token_uri")]
        public string TokenUri { get; init; } = "https://oauth2.googleapis.com/token";
    }

    private sealed record OAuthToken
    {
        [JsonPropertyName("access_token")]
        public required string AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; } = 3600;
    }

    private sealed record FcmSent
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    private sealed record FcmFailure
    {
        [JsonPropertyName("error")]
        public FcmError? Error { get; init; }
    }

    private sealed record FcmError
    {
        [JsonPropertyName("status")]
        public string? Status { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }

        [JsonPropertyName("details")]
        public List<FcmErrorDetail>? Details { get; init; }
    }

    private sealed record FcmErrorDetail
    {
        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; init; }
    }
}
