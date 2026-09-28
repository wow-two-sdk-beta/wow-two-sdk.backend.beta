using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound;
using WoW.Two.Sdk.Backend.Beta.Messaging.Webhooks.Inbound.Validators;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Webhooks;

/// <summary>The built-in signature schemes against their providers' published examples and their failure modes.</summary>
public sealed class WebhookSignatureValidatorTests
{
    [Fact]
    public void Standard_ShouldMatchThePublishedExample_UnderBothHeaderFamilies()
    {
        var body = Encoding.UTF8.GetBytes("{\"test\": 2432232314}");
        var receiver = Receiver("whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw");
        var at = DateTimeOffset.FromUnixTimeSeconds(1614265330);
        var signature = "v1a,ignored v1,g0hM9SsE+OTPJTGt/tmIKtSyZlE3uFJELVlNIOLJ1OE=";

        var standard = new StandardWebhookSignatureValidator().Validate(
            Headers(("webhook-id", "msg_p5jXN8AQM9LWM0D4loKWxJek"), ("webhook-timestamp", "1614265330"), ("webhook-signature", signature)), body, receiver, at);
        standard.Succeeded.Should().BeTrue();
        standard.DeliveryId.Should().Be("msg_p5jXN8AQM9LWM0D4loKWxJek");
        standard.Timestamp.Should().Be(at);

        new StandardWebhookSignatureValidator().Validate(
            Headers(("svix-id", "msg_p5jXN8AQM9LWM0D4loKWxJek"), ("svix-timestamp", "1614265330"), ("svix-signature", signature)), body, receiver, at)
            .Succeeded.Should().BeTrue();
        new StandardWebhookSignatureValidator().Validate(
            Headers(("webhook-id", "msg_other"), ("webhook-timestamp", "1614265330"), ("webhook-signature", signature)), body, receiver, at)
            .Failure.Should().Be(WebhookSignatureFailure.Mismatch, "the id is signed");
        new StandardWebhookSignatureValidator().Validate(
            Headers(("webhook-id", "msg_p5jXN8AQM9LWM0D4loKWxJek"), ("webhook-timestamp", "1614265330"), ("webhook-signature", signature)), body, receiver, at.AddMinutes(6))
            .Failure.Should().Be(WebhookSignatureFailure.Stale);
    }

    [Fact]
    public void GitHub_ShouldMatchThePublishedExample()
    {
        var body = Encoding.UTF8.GetBytes("Hello, World!");
        var headers = Headers(
            ("X-Hub-Signature-256", "sha256=757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17"),
            ("X-GitHub-Delivery", "72d3162e-cc78-11e3-81ab-4c9367dc0958"),
            ("X-GitHub-Event", "push"));
        var validator = new GitHubWebhookSignatureValidator();

        var result = validator.Validate(headers, body, Receiver("rotated-out", "It's a Secret to Everybody"), DateTimeOffset.UnixEpoch);
        result.Succeeded.Should().BeTrue();
        result.DeliveryId.Should().Be("72d3162e-cc78-11e3-81ab-4c9367dc0958");
        result.EventType.Should().Be("push");

        validator.Validate(headers, body, Receiver("wrong"), DateTimeOffset.UnixEpoch).Failure.Should().Be(WebhookSignatureFailure.Mismatch);
        validator.Validate(Headers(), body, Receiver("wrong"), DateTimeOffset.UnixEpoch).Failure.Should().Be(WebhookSignatureFailure.Missing);
        validator.Validate(Headers(("X-Hub-Signature-256", "md5=00")), body, Receiver("wrong"), DateTimeOffset.UnixEpoch).Failure.Should().Be(WebhookSignatureFailure.Malformed);
    }

    [Fact]
    public void Slack_ShouldMatchThePublishedExample_WithinTheReplayWindow()
    {
        var body = Encoding.UTF8.GetBytes("token=xyzz0WbapA4vBCDEFasx0q6G&team_id=T1DC2JH3J&team_domain=testteamnow&channel_id=G8PSS9T3V&channel_name=foobar&user_id=U2CERLKJA&user_name=roadrunner&command=%2Fwebhook-collect&text=&response_url=https%3A%2F%2Fhooks.slack.com%2Fcommands%2FT1DC2JH3J%2F397700885554%2F96rGlfmibIGlgcZRskXaIFfN&trigger_id=398738663015.47445629121.803a0bc887a14d10d2c447fce8b6703c");
        var headers = Headers(
            ("X-Slack-Signature", "v0=a2114d57b48eac39b9ad189dd8316235a7b4a8d21a10bd27519666489c69b503"),
            ("X-Slack-Request-Timestamp", "1531420618"));
        var receiver = Receiver("8f742231b10e8888abcd99yyyzzz85a5");
        var at = DateTimeOffset.FromUnixTimeSeconds(1531420618);

        new SlackWebhookSignatureValidator().Validate(headers, body, receiver, at.AddSeconds(30)).Succeeded.Should().BeTrue();
        new SlackWebhookSignatureValidator().Validate(headers, body, receiver, at.AddMinutes(10)).Failure.Should().Be(WebhookSignatureFailure.Stale);
        receiver.Tolerance = TimeSpan.Zero;
        new SlackWebhookSignatureValidator().Validate(headers, body, receiver, at.AddDays(1)).Succeeded.Should().BeTrue("a zero tolerance turns the check off");
    }

    [Fact]
    public void Stripe_ShouldAcceptAnyListedSignatureAndReadTheEventFromThePayload()
    {
        const string Secret = "whsec_test_secret";
        var body = Encoding.UTF8.GetBytes("{\"id\":\"evt_1\",\"object\":\"event\",\"type\":\"invoice.paid\",\"data\":{\"object\":{}}}");
        var at = DateTimeOffset.FromUnixTimeSeconds(1_750_000_000);
        var valid = Hex(Secret, "1750000000." + Encoding.UTF8.GetString(body));
        var validator = new StripeWebhookSignatureValidator();

        var result = validator.Validate(Headers(("Stripe-Signature", $"t=1750000000,v1={new string('0', 64)},v1={valid},v0=legacy")), body, Receiver(Secret), at);
        result.Succeeded.Should().BeTrue();
        result.DeliveryId.Should().Be("evt_1");
        result.EventType.Should().Be("invoice.paid");

        validator.Validate(Headers(("Stripe-Signature", $"t=1750000000,t=1750000001,v1={valid}")), body, Receiver(Secret), at).Failure.Should().Be(WebhookSignatureFailure.Malformed);
        validator.Validate(Headers(("Stripe-Signature", $"t=1750000000,v1={valid}")), [.. body, (byte)' '], Receiver(Secret), at).Failure.Should().Be(WebhookSignatureFailure.Mismatch);
    }

    [Fact]
    public void PaddleShopifyAndTelegram_ShouldValidateTheirOwnHeaders()
    {
        const string Secret = "pdl_ntfset_secret";
        var body = Encoding.UTF8.GetBytes("{\"event_id\":\"evt_01h\",\"event_type\":\"transaction.completed\",\"data\":{}}");
        var at = DateTimeOffset.FromUnixTimeSeconds(1_671_552_777);

        var paddle = new PaddleWebhookSignatureValidator().Validate(
            Headers(("Paddle-Signature", $"ts=1671552777;h1={Hex(Secret, "1671552777:" + Encoding.UTF8.GetString(body))}")), body, Receiver(Secret), at);
        paddle.Succeeded.Should().BeTrue();
        paddle.EventType.Should().Be("transaction.completed");

        var shopify = new ShopifyWebhookSignatureValidator().Validate(
            Headers(("X-Shopify-Hmac-Sha256", Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), body))), ("X-Shopify-Webhook-Id", "b54557e4"), ("X-Shopify-Topic", "orders/create"), ("X-Shopify-Triggered-At", "2023-03-29T18:00:27.877041743Z")),
            body, Receiver(Secret), at.AddDays(2));
        shopify.Succeeded.Should().BeTrue("Shopify retries with the original time, so it is not a replay window");
        shopify.DeliveryId.Should().Be("b54557e4");
        shopify.Timestamp.Should().Be(new DateTimeOffset(2023, 3, 29, 18, 0, 27, TimeSpan.Zero).AddTicks(8770417));

        var update = Encoding.UTF8.GetBytes("{\"update_id\":918273,\"message\":{\"text\":\"hi\"}}");
        var telegram = new TelegramWebhookSignatureValidator().Validate(Headers(("X-Telegram-Bot-Api-Secret-Token", "tg-secret")), update, Receiver("old", "tg-secret"), at);
        telegram.Succeeded.Should().BeTrue();
        telegram.DeliveryId.Should().Be("918273");
        telegram.EventType.Should().Be("message");
        new TelegramWebhookSignatureValidator().Validate(Headers(("X-Telegram-Bot-Api-Secret-Token", "tg-secreT")), update, Receiver("tg-secret"), at)
            .Failure.Should().Be(WebhookSignatureFailure.Mismatch);
    }

    [Fact]
    public void Wow2_ShouldAcceptWhatTheOutboundHasherSigns()
    {
        const string Secret = "whsec_shared";
        var body = Encoding.UTF8.GetBytes("{\"orderId\":42}");
        var signature = new WebhookSignatureHasher().Create(Secret, "1750000000", body);
        var headers = Headers(
            (WebhookHeaderConstants.Signature, signature),
            (WebhookHeaderConstants.Timestamp, "1750000000"),
            (WebhookHeaderConstants.Id, "delivery-7"),
            (WebhookHeaderConstants.Event, "OrderPlaced"));

        var result = new Wow2WebhookSignatureValidator().Validate(headers, body, Receiver(Secret), DateTimeOffset.FromUnixTimeSeconds(1_750_000_060));
        result.Succeeded.Should().BeTrue();
        result.DeliveryId.Should().Be("delivery-7");
        result.EventType.Should().Be("OrderPlaced");
    }

    private static WebhookReceiverOptions Receiver(params string[] secrets)
    {
        var receiver = new WebhookReceiverOptions();
        receiver.Secrets.AddRange(secrets);
        return receiver;
    }

    private static HeaderDictionary Headers(params (string Name, string Value)[] headers)
    {
        var dictionary = new HeaderDictionary();
        foreach (var (name, value) in headers)
            dictionary[name] = value;

        return dictionary;
    }

    private static string Hex(string secret, string content)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(content)));
}
