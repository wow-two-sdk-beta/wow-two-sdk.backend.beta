using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.Apns;

/// <summary>
/// Integrates Apple Push Notification service over HTTP/2 with token-based authentication: an ES256 provider token
/// signed with the <c>.p8</c> key, reused for 50 minutes. A <c>410</c> or bad-token answer marks the token invalid.
/// </summary>
public sealed class ApnsPushBroker : IPushBroker, IDisposable
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.push.apns";

    private static readonly TimeSpan TokenReuse = TimeSpan.FromMinutes(50);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApnsPushOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ECDsa _key;
    private readonly Lock _gate = new();
    private (string Token, DateTimeOffset IssuedAt)? _providerToken;

    /// <summary>Create the broker over the configured key.</summary>
    /// <param name="httpClientFactory">Creates the named HTTP/2 client.</param>
    /// <param name="options">Credentials and topic.</param>
    /// <param name="timeProvider">The clock provider tokens are stamped with.</param>
    public ApnsPushBroker(IHttpClientFactory httpClientFactory, ApnsPushOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
        _key = ECDsa.Create();
        _key.ImportFromPem(options.PrivateKeyPem);
    }

    /// <inheritdoc />
    public async Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var http = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"3/device/{Uri.EscapeDataString(message.DeviceToken)}")
            {
                Version = HttpVersion.Version20,
                VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
                Content = new StringContent(Payload(message).ToJsonString(), Encoding.UTF8, "application/json"),
            };
            var silent = message.Title is null && message.Body is null;
            request.Headers.TryAddWithoutValidation("authorization", $"bearer {ProviderToken()}");
            request.Headers.TryAddWithoutValidation("apns-topic", _options.BundleId);
            request.Headers.TryAddWithoutValidation("apns-push-type", silent ? "background" : "alert");
            request.Headers.TryAddWithoutValidation("apns-priority", silent ? "5" : "10");
            if (message.CollapseKey is not null)
                request.Headers.TryAddWithoutValidation("apns-collapse-id", message.CollapseKey);
            if (message.TimeToLive is { } ttl)
                request.Headers.TryAddWithoutValidation("apns-expiration", (_timeProvider.GetUtcNow() + ttl).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));

            using var response = await http.SendAsync(request, cancellationToken);
            var apnsId = response.Headers.TryGetValues("apns-id", out var ids) ? ids.FirstOrDefault() : null;
            if (response.IsSuccessStatusCode)
                return new PushSendResult { Success = true, ProviderMessageId = apnsId };

            var reason = (await response.Content.ReadFromJsonAsync<ApnsError>(cancellationToken))?.Reason ?? "Unknown";
            if (reason == "ExpiredProviderToken")
                lock (_gate)
                    _providerToken = null;

            return new PushSendResult
            {
                Success = false,
                ProviderMessageId = apnsId,
                FailureReason = $"apns_{(int)response.StatusCode}: {reason}",
                TokenInvalid = response.StatusCode == HttpStatusCode.Gone || reason is "BadDeviceToken" or "Unregistered" or "DeviceTokenNotForTopic",
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new PushSendResult { Success = false, FailureReason = exception.Message };
        }
    }

    /// <inheritdoc />
    public void Dispose() => _key.Dispose();

    private static JsonObject Payload(PushMessage message)
    {
        var aps = new JsonObject();
        if (message.Title is not null || message.Body is not null)
            aps["alert"] = new JsonObject { ["title"] = message.Title, ["body"] = message.Body };
        else
            aps["content-available"] = 1;
        if (message.Badge is { } badge)
            aps["badge"] = badge;
        if (message.Sound is not null)
            aps["sound"] = message.Sound;

        var payload = new JsonObject { ["aps"] = aps };
        foreach (var (key, value) in message.Data ?? new Dictionary<string, string>())
            payload[key] = value;
        return payload;
    }

    private string ProviderToken()
    {
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_providerToken is { } cached && now - cached.IssuedAt < TokenReuse)
                return cached.Token;

            var header = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["alg"] = "ES256", ["kid"] = _options.KeyId }));
            var claims = Encode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object> { ["iss"] = _options.TeamId, ["iat"] = now.ToUnixTimeSeconds() }));
            var signingInput = $"{header}.{claims}";
            var signature = _key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var token = $"{signingInput}.{Base64Url.EncodeToString(signature)}";
            _providerToken = (token, now);
            return token;
        }
    }

    private static string Encode(byte[] json) => Base64Url.EncodeToString(json);

    private sealed record ApnsError
    {
        [JsonPropertyName("reason")]
        public string? Reason { get; init; }
    }
}
