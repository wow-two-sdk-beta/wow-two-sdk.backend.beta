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

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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
