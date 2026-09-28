using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Comms.Push;
using WoW.Two.Sdk.Backend.Beta.Comms.Push.Apns;
using WoW.Two.Sdk.Backend.Beta.Comms.Push.Fcm;
using WoW.Two.Sdk.Backend.Beta.Comms.Push.WebPush;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Comms;

/// <summary>Push brokers against scripted gateways: signed provider tokens, token reuse, payload shape, invalid-token mapping.</summary>
public sealed class PushBrokerTests
{
    private static readonly PushMessage Alert = new()
    {
        DeviceToken = "device-1",
        Title = "Order shipped",
        Body = "Arrives Friday",
        Data = new Dictionary<string, string> { ["orderId"] = "42" },
        Badge = 3,
        CollapseKey = "order-42",
    };

    [Fact]
    public async Task Apns_ShouldSendASignedReusedProviderTokenAndTheAlertPayload()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var gateway = new ScriptedGateway(_ =>
        {
            var ok = new HttpResponseMessage(HttpStatusCode.OK);
            ok.Headers.Add("apns-id", "A-1");
            return ok;
        });
        var broker = Build(services => services.AddApnsPushBroker(o =>
        {
            o.TeamId = "TEAM123";
            o.KeyId = "KEY456";
            o.PrivateKeyPem = key.ExportPkcs8PrivateKeyPem();
            o.BundleId = "uz.acme.app";
            o.BaseAddress = new Uri("https://apns.test/");
        }), gateway);

        (await broker.SendAsync(Alert)).ProviderMessageId.Should().Be("A-1");
        await broker.SendAsync(Alert);

        var first = gateway.Requests[0];
        first.Path.Should().Be("/3/device/device-1");
        first.Headers["apns-topic"].Should().Be("uz.acme.app");
        first.Headers["apns-push-type"].Should().Be("alert");
        first.Headers["apns-collapse-id"].Should().Be("order-42");
        gateway.Requests[1].Headers["authorization"].Should().Be(first.Headers["authorization"]);

        var jwt = first.Headers["authorization"]["bearer ".Length..];
        VerifyEs256(jwt, key).Should().BeTrue();
        Claims(jwt, header: true)["kid"]!.GetValue<string>().Should().Be("KEY456");
        Claims(jwt)["iss"]!.GetValue<string>().Should().Be("TEAM123");

        var body = JsonNode.Parse(first.Body)!;
        body["aps"]!["alert"]!["title"]!.GetValue<string>().Should().Be("Order shipped");
        body["aps"]!["badge"]!.GetValue<int>().Should().Be(3);
        body["orderId"]!.GetValue<string>().Should().Be("42");
    }

    [Fact]
    public async Task Apns_ShouldMarkAnUnregisteredToken()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var gateway = new ScriptedGateway(_ => Json(HttpStatusCode.Gone, """{"reason":"Unregistered"}"""));
        var broker = Build(services => services.AddApnsPushBroker(o =>
        {
            o.TeamId = "T";
            o.KeyId = "K";
            o.PrivateKeyPem = key.ExportPkcs8PrivateKeyPem();
            o.BundleId = "b";
            o.BaseAddress = new Uri("https://apns.test/");
        }), gateway);

        var result = await broker.SendAsync(new PushMessage { DeviceToken = "gone" });

        result.Should().BeEquivalentTo(new PushSendResult { Success = false, FailureReason = "apns_410: Unregistered", TokenInvalid = true });
        gateway.Requests.Single().Headers["apns-push-type"].Should().Be("background");
    }

    [Fact]
    public async Task Fcm_ShouldExchangeOneSignedAssertionAndSendTheV1Message()
    {
        using var key = RSA.Create(2048);
        var tokenRequests = 0;
        var gateway = new ScriptedGateway(request => request.Path switch
        {
            "/token" => Json(HttpStatusCode.OK, $$"""{"access_token":"ya29.token-{{++tokenRequests}}","expires_in":3600,"token_type":"Bearer"}"""),
            "/v1/projects/acme-app/messages:send" => Json(HttpStatusCode.OK, """{"name":"projects/acme-app/messages/0:1"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var broker = Build(services => services.AddFcmPushBroker(o =>
        {
            o.ServiceAccountJson = ServiceAccount(key);
            o.BaseAddress = new Uri("https://fcm.test/");
        }), gateway);

        (await broker.SendAsync(Alert)).ProviderMessageId.Should().Be("projects/acme-app/messages/0:1");
        (await broker.SendAsync(Alert)).Success.Should().BeTrue();

        tokenRequests.Should().Be(1);
        var assertion = Uri.UnescapeDataString(gateway.Requests[0].Body.Split('&').Single(part => part.StartsWith("assertion=", StringComparison.Ordinal))["assertion=".Length..]);
        VerifyRs256(assertion, key).Should().BeTrue();
        Claims(assertion)["scope"]!.GetValue<string>().Should().Be("https://www.googleapis.com/auth/firebase.messaging");
        Claims(assertion)["iss"]!.GetValue<string>().Should().Be("push@acme-app.iam.gserviceaccount.com");

        var send = gateway.Requests.Last();
        send.Headers["authorization"].Should().Be("Bearer ya29.token-1");
        var message = JsonNode.Parse(send.Body)!["message"]!;
        message["token"]!.GetValue<string>().Should().Be("device-1");
        message["notification"]!["body"]!.GetValue<string>().Should().Be("Arrives Friday");
        message["android"]!["collapse_key"]!.GetValue<string>().Should().Be("order-42");
        message["apns"]!["payload"]!["aps"]!["badge"]!.GetValue<int>().Should().Be(3);
    }

    [Fact]
    public async Task Fcm_ShouldMarkAnUnregisteredToken()
    {
        using var key = RSA.Create(2048);
        var gateway = new ScriptedGateway(request => request.Path == "/token"
            ? Json(HttpStatusCode.OK, """{"access_token":"t","expires_in":3600}""")
            : Json(HttpStatusCode.NotFound, """{"error":{"code":404,"message":"Requested entity was not found.","status":"NOT_FOUND","details":[{"@type":"type.googleapis.com/google.firebase.fcm.v1.FcmError","errorCode":"UNREGISTERED"}]}}"""));
        var broker = Build(services => services.AddFcmPushBroker(o =>
        {
            o.ServiceAccountJson = ServiceAccount(key);
            o.BaseAddress = new Uri("https://fcm.test/");
        }), gateway);

        var result = await broker.SendAsync(new PushMessage { DeviceToken = "stale", Title = "t" });

        result.TokenInvalid.Should().BeTrue();
        result.FailureReason.Should().Be("fcm_UNREGISTERED: Requested entity was not found.");
    }

    [Fact]
    public async Task WebPush_ShouldEncryptForTheSubscriptionAndSignVapid()
    {
        using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var auth = RandomNumberGenerator.GetBytes(16);
        var vapidParameters = vapid.ExportParameters(true);
        var browserPublic = Point(browser.ExportParameters(false));
        byte[]? captured = null;
        var gateway = new ScriptedGateway(_ => new HttpResponseMessage(HttpStatusCode.Created));
        gateway.Capture = bytes => captured = bytes;
        var broker = Build(services => services.AddWebPushBroker(o =>
        {
            o.Subject = "mailto:ops@acme.uz";
            o.PublicKey = Base64Url.EncodeToString(Point(vapidParameters));
            o.PrivateKey = Base64Url.EncodeToString(vapidParameters.D);
        }), gateway);
        var subscription = JsonSerializer.Serialize(new
        {
            endpoint = "https://push.test/send/abc",
            keys = new { p256dh = Base64Url.EncodeToString(browserPublic), auth = Base64Url.EncodeToString(auth) },
        });

        (await broker.SendAsync(Alert with { DeviceToken = subscription, CollapseKey = "order-42" })).Success.Should().BeTrue();

        var sent = gateway.Requests.Single();
        sent.Path.Should().Be("/send/abc");
        sent.Headers["topic"].Should().Be("order-42");
        var authorization = sent.Headers["authorization"];
        var jwt = authorization["vapid t=".Length..authorization.IndexOf(',', StringComparison.Ordinal)];
        VerifyEs256(jwt, vapid).Should().BeTrue();
        Claims(jwt)["aud"]!.GetValue<string>().Should().Be("https://push.test");
        Claims(jwt)["sub"]!.GetValue<string>().Should().Be("mailto:ops@acme.uz");

        var payload = JsonNode.Parse(Decrypt(captured!, browser, browserPublic, auth))!;
        payload["title"]!.GetValue<string>().Should().Be("Order shipped");
        payload["data"]!["orderId"]!.GetValue<string>().Should().Be("42");
    }

    [Fact]
    public async Task WebPush_ShouldMarkAnExpiredSubscription()
    {
        using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var browser = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var vapidParameters = vapid.ExportParameters(true);
        var gateway = new ScriptedGateway(_ => new HttpResponseMessage(HttpStatusCode.Gone));
        var broker = Build(services => services.AddWebPushBroker(o =>
        {
            o.Subject = "https://acme.uz";
            o.PublicKey = Base64Url.EncodeToString(Point(vapidParameters));
            o.PrivateKey = Base64Url.EncodeToString(vapidParameters.D);
        }), gateway);
        var subscription = JsonSerializer.Serialize(new
        {
            endpoint = "https://push.test/send/old",
            keys = new { p256dh = Base64Url.EncodeToString(Point(browser.ExportParameters(false))), auth = Base64Url.EncodeToString(new byte[16]) },
        });

        (await broker.SendAsync(new PushMessage { DeviceToken = subscription })).TokenInvalid.Should().BeTrue();
        (await broker.SendAsync(new PushMessage { DeviceToken = "not json" })).TokenInvalid.Should().BeTrue();
    }

    private static byte[] Point(ECParameters parameters) => [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];

    /// <summary>The user agent's side of RFC 8291: derive the content key from the record header and decrypt.</summary>
    private static byte[] Decrypt(byte[] body, ECDiffieHellman receiver, byte[] receiverPublic, byte[] auth)
    {
        var salt = body[..16];
        var idLength = body[20];
        var senderPublic = body[21..(21 + idLength)];
        var cipher = body[(21 + idLength)..];

        using var sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = senderPublic[1..33], Y = senderPublic[33..65] },
        });
        var shared = receiver.DeriveRawSecretAgreement(sender.PublicKey);
        byte[] keyInfo = [.. "WebPush: info\0"u8, .. receiverPublic, .. senderPublic];
        var inputKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, auth, keyInfo);
        var pseudoRandomKey = HKDF.Extract(HashAlgorithmName.SHA256, inputKey, salt);
        var contentKey = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 16, "Content-Encoding: aes128gcm\0"u8.ToArray());
        var nonce = HKDF.Expand(HashAlgorithmName.SHA256, pseudoRandomKey, 12, "Content-Encoding: nonce\0"u8.ToArray());

        var padded = new byte[cipher.Length - 16];
        using var aes = new AesGcm(contentKey, 16);
        aes.Decrypt(nonce, cipher[..^16], cipher[^16..], padded);
        return padded[..Array.LastIndexOf(padded, (byte)0x02)];
    }

    private static IPushBroker Build(Func<IServiceCollection, IHttpClientBuilder> register, ScriptedGateway gateway)
    {
        var services = new ServiceCollection();
        register(services).ConfigurePrimaryHttpMessageHandler(() => gateway);
        return services.BuildServiceProvider().GetRequiredService<IPushBroker>();
    }

    private static string ServiceAccount(RSA key) => JsonSerializer.Serialize(new Dictionary<string, string>
    {
        ["type"] = "service_account",
        ["project_id"] = "acme-app",
        ["client_email"] = "push@acme-app.iam.gserviceaccount.com",
        ["private_key"] = key.ExportPkcs8PrivateKeyPem(),
        ["token_uri"] = "https://oauth.test/token",
    });

    private static bool VerifyEs256(string jwt, ECDsa key)
    {
        var parts = jwt.Split('.');
        return key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static bool VerifyRs256(string jwt, RSA key)
    {
        var parts = jwt.Split('.');
        return key.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static JsonNode Claims(string jwt, bool header = false)
        => JsonNode.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[header ? 0 : 1]))!;

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record GatewayRequest
    {
        public required string Path { get; init; }

        public required Dictionary<string, string> Headers { get; init; }

        public required string Body { get; init; }
    }

    /// <summary>Answers each request from a script and records what the broker sent.</summary>
    private sealed class ScriptedGateway(Func<GatewayRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<GatewayRequest> Requests { get; } = [];

        public Action<byte[]>? Capture { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Capture is not null && request.Content is not null)
                Capture(await request.Content.ReadAsByteArrayAsync(cancellationToken));

            var recorded = new GatewayRequest
            {
                Path = request.RequestUri!.AbsolutePath,
                Headers = request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(",", h.Value)),
                Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken),
            };
            Requests.Add(recorded);
            return respond(recorded);
        }
    }
}
