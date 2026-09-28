using System.Buffers.Binary;
using System.Buffers.Text;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WoW.Two.Sdk.Backend.Beta.Comms.Push.WebPush;

/// <summary>
/// Integrates the Web Push protocol (RFC 8030) with VAPID (RFC 8292) and <c>aes128gcm</c> payload encryption (RFC 8291).
/// The device token is the browser's <c>PushSubscription</c> JSON; the payload is JSON the service worker reads.
/// </summary>
public sealed class WebPushBroker : IPushBroker, IDisposable
{
    /// <summary>The named <see cref="HttpClient"/> this broker uses.</summary>
    public const string HttpClientName = "wow2.push.webpush";

    private const int RecordSize = 4096;
    private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromDays(28);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebPushOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ECDsa _vapidKey;

    /// <summary>Create the broker over the VAPID key pair.</summary>
    /// <param name="httpClientFactory">Creates the named client.</param>
    /// <param name="options">VAPID subject and keys.</param>
    /// <param name="timeProvider">The clock VAPID tokens are stamped with.</param>
    public WebPushBroker(IHttpClientFactory httpClientFactory, WebPushOptions options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _httpClientFactory = httpClientFactory;
        _options = options;
        _timeProvider = timeProvider;
        var point = Base64Url.DecodeFromChars(options.PublicKey);
        _vapidKey = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.DecodeFromChars(options.PrivateKey),
            Q = new ECPoint { X = point[1..33], Y = point[33..65] },
        });
    }

    /// <inheritdoc />
    public async Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var subscription = JsonSerializer.Deserialize<Subscription>(message.DeviceToken)
                ?? throw new FormatException("The device token is not a PushSubscription.");
            var endpoint = new Uri(subscription.Endpoint);
            var body = Encrypt(
                Encoding.UTF8.GetBytes(Payload(message).ToJsonString()),
                Base64Url.DecodeFromChars(subscription.Keys.P256dh),
                Base64Url.DecodeFromChars(subscription.Keys.Auth));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
            request.Headers.TryAddWithoutValidation("TTL", ((long)(message.TimeToLive ?? DefaultTimeToLive).TotalSeconds).ToString(CultureInfo.InvariantCulture));
            request.Headers.TryAddWithoutValidation("Urgency", message.Title is null && message.Body is null ? "low" : "normal");
            if (message.CollapseKey is { Length: <= 32 } topic && topic.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                request.Headers.TryAddWithoutValidation("Topic", topic);
            request.Headers.TryAddWithoutValidation("Authorization", $"vapid t={VapidToken(endpoint)}, k={_options.PublicKey}");

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return new PushSendResult { Success = true, ProviderMessageId = response.Headers.Location?.ToString() };

            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            return new PushSendResult
            {
                Success = false,
                FailureReason = $"webpush_{(int)response.StatusCode}: {detail}",
                TokenInvalid = response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return new PushSendResult { Success = false, FailureReason = exception.Message, TokenInvalid = exception is FormatException or JsonException };
        }
    }

    /// <inheritdoc />
    public void Dispose() => _vapidKey.Dispose();

    private static JsonObject Payload(PushMessage message)
    {
        var payload = new JsonObject { ["title"] = message.Title, ["body"] = message.Body };
        if (message.Data is { Count: > 0 } data)
            payload["data"] = new JsonObject(data.Select(pair => KeyValuePair.Create(pair.Key, (JsonNode?)pair.Value)));
        if (message.Badge is { } badge)
            payload["badge"] = badge;
        if (message.CollapseKey is not null)
            payload["tag"] = message.CollapseKey;
        return payload;
    }

    /// <summary>Encrypts one record: salt, record size, sender key, then AES-128-GCM over the padded payload.</summary>
    private static byte[] Encrypt(byte[] plaintext, byte[] receiverKey, byte[] authSecret)
    {
        using var sender = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        using var receiver = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = receiverKey[1..33], Y = receiverKey[33..65] },
        });
        var senderParameters = sender.ExportParameters(false);
        byte[] senderKey = [0x04, .. senderParameters.Q.X!, .. senderParameters.Q.Y!];

        var sharedSecret = sender.DeriveRawSecretAgreement(receiver.PublicKey);
        byte[] keyInfo = [.. "WebPush: info\0"u8, .. receiverKey, .. senderKey];
        var inputKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, authSecret, keyInfo);

        var salt = RandomNumberGenerator.GetBytes(16);
        var pseudoRandomKey = HKDF.Extract(HashAlgorithmName.SHA256, inputKey, salt);
        var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 16, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 12, "Content-Encoding: nonce\0"u8.ToArray());

        byte[] padded = [.. plaintext, 0x02];
        var body = new byte[16 + 4 + 1 + senderKey.Length + padded.Length + 16];
        salt.CopyTo(body, 0);
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(16, 4), RecordSize);
        body[20] = (byte)senderKey.Length;
        senderKey.CopyTo(body, 21);

        var cipherStart = 21 + senderKey.Length;
        using var aes = new AesGcm(contentKey, 16);
        aes.Encrypt(nonce, padded, body.AsSpan(cipherStart, padded.Length), body.AsSpan(cipherStart + padded.Length, 16));
        return body;
    }

    private string VapidToken(Uri endpoint)
    {
        var header = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["typ"] = "JWT", ["alg"] = "ES256" }));
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = endpoint.GetLeftPart(UriPartial.Authority),
            ["exp"] = _timeProvider.GetUtcNow().AddHours(12).ToUnixTimeSeconds(),
            ["sub"] = _options.Subject,
        }));
        var signingInput = $"{header}.{claims}";
        var signature = _vapidKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
    }

    private sealed record Subscription
    {
        [JsonPropertyName("endpoint")]
        public required string Endpoint { get; init; }

        [JsonPropertyName("keys")]
        public required SubscriptionKeys Keys { get; init; }
    }

    private sealed record SubscriptionKeys
    {
        [JsonPropertyName("p256dh")]
        public required string P256dh { get; init; }

        [JsonPropertyName("auth")]
        public required string Auth { get; init; }
    }
}
