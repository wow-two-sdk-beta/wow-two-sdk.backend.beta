using System.Globalization;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WoW.Two.Sdk.Backend.Beta.Comms.Email;
using WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Email;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.TelegramGateway;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.WhatsApp;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.Otp;

/// <summary>Code specs, per-culture wording and the email, WhatsApp and Telegram Gateway channels.</summary>
public sealed class OtpCodeAndMessageTests
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru");

    [Theory]
    [InlineData(OtpCodeKind.Numeric, 4, "^[0-9]{4}$")]
    [InlineData(OtpCodeKind.Alphanumeric, 8, "^[A-HJ-NP-Z2-9]{8}$")]
    [InlineData(OtpCodeKind.Letters, 12, "^[A-HJ-NP-Z]{12}$")]
    public void Generator_ShouldFollowTheSpec(OtpCodeKind kind, int length, string pattern)
    {
        var generator = new OtpCodeGenerator();

        Enumerable.Range(0, 50).Select(_ => generator.Generate(new OtpCodeSpec { Kind = kind, Length = length }))
            .Should().AllSatisfy(code => code.Should().MatchRegex(pattern));
        FluentActions.Invoking(() => generator.Generate(new OtpCodeSpec { Length = 3 })).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Service_ShouldTakeTheSpecLifetimeAndForgiveCaseSpacesAndDashes()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero));
        await using var provider = new ServiceCollection().AddSingleton<TimeProvider>(time).AddOtpService().BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var otp = scope.ServiceProvider.GetRequiredService<IOtpService>();

        var created = await otp.CreateAsync("user-1", "two-factor", new OtpCodeSpec { Kind = OtpCodeKind.Alphanumeric, Length = 6, Lifetime = TimeSpan.FromMinutes(15) });
        created.ExpiresAt.Should().Be(time.GetUtcNow().AddMinutes(15));
        (await otp.CreateAsync("user-1", "default-spec")).Code.Should().MatchRegex("^[0-9]{6}$");

        var typed = $" {created.Code![..3].ToLowerInvariant()}-{created.Code[3..]} ";
        (await otp.VerifyAsync("user-1", typed, "two-factor")).Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("en", 1, "123456 is your Acme verification code. It expires in 1 minute. Do not share it with anyone.")]
    [InlineData("en-GB", 5, "123456 is your Acme verification code. It expires in 5 minutes. Do not share it with anyone.")]
    [InlineData("ru", 1, "123456 — ваш код подтверждения Acme. Код действует 1 минуту. Никому его не сообщайте.")]
    [InlineData("ru-RU", 3, "123456 — ваш код подтверждения Acme. Код действует 3 минуты. Никому его не сообщайте.")]
    [InlineData("ru", 5, "123456 — ваш код подтверждения Acme. Код действует 5 минут. Никому его не сообщайте.")]
    [InlineData("uz-Latn-UZ", 5, "123456 — Acme tasdiqlash kodingiz. Kod 5 daqiqa amal qiladi. Uni hech kimga bermang.")]
    [InlineData("de", 5, "123456 is your Acme verification code. It expires in 5 minutes. Do not share it with anyone.")]
    public void Formatter_ShouldWordTheCodeInTheRecipientsCulture(string culture, int minutes, string expected)
    {
        var formatter = Formatter(o => o.Messages.AppName = "Acme");

        formatter.Format("two-factor", "sms", "123456", TimeSpan.FromMinutes(minutes), CultureInfo.GetCultureInfo(culture)).Text.Should().Be(expected);
    }

    [Fact]
    public void Formatter_ShouldPreferTheMostSpecificConfiguredTemplate_WithTheHostSectionLast()
    {
        var formatter = Formatter(
            o =>
            {
                o.Messages.AppName = "Acme";
                o.Messages.Templates["ru"] = new Dictionary<string, string> { ["two-factor.sms"] = "Код {app}: {code}", ["two-factor.subject"] = "Вход в {app}" };
            },
            new Dictionary<string, string?> { ["Identity:Otp:Messages:AppName"] = "Haven" });

        formatter.Format("two-factor", "sms", "123456", TimeSpan.FromMinutes(5), Russian).Text.Should().Be("Код Haven: 123456");
        var email = formatter.Format("two-factor", "email", "123456", TimeSpan.FromMinutes(5), Russian);
        email.Text.Should().StartWith("123456 — ваш код подтверждения Haven.");
        email.Subject.Should().Be("Вход в Haven");
    }

    [Fact]
    public async Task EmailChannel_ShouldSendTheWordedCodeFromTheConfiguredSender()
    {
        var mail = new CapturingEmailBroker();
        await using var provider = new ServiceCollection()
            .AddSingleton<IEmailBroker>(mail)
            .AddEmailOtpDelivery(o => { o.From = "security@acme.test"; o.FromName = "Acme"; })
            .BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IOtpDeliveryHandlerFactory>().Create(OtpChannelNameConstants.Email)!;

        var result = await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "ana@acme.test", Code = "AB3K9Q", Scope = "login", Culture = "en" });

        result.Success.Should().BeTrue();
        var message = mail.Sent.Single();
        message.To.Single().Address.Should().Be("ana@acme.test");
        message.From.Should().BeEquivalentTo(new EmailAddress { Address = "security@acme.test", DisplayName = "Acme" });
        message.Subject.Should().Be("Your App verification code");
        message.TextBody.Should().StartWith("AB3K9Q is your App verification code.");
        (await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "not-an-address", Code = "1", Scope = "login" })).FailureReason.Should().Be("invalid_email_address");
    }

    [Fact]
    public async Task WhatsAppChannel_ShouldSendTheTemplateInTheRecipientsLanguage_ElseFreeText()
    {
        var whatsApp = new CapturingWhatsAppBroker();
        var services = new ServiceCollection()
            .AddSingleton<IWhatsAppBroker>(whatsApp)
            .AddSingleton<IWhatsAppBrokerFactory>(new SingleWhatsAppBrokerFactory(whatsApp))
            .AddWhatsAppOtpDelivery(o =>
            {
                o.TemplateName = "auth_code";
                o.TemplateLanguages["ru"] = "ru";
            });
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IOtpDeliveryHandlerFactory>().Create("WhatsApp")!;

        (await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "+998901234567", Code = "123456", Scope = "two-factor", Culture = "ru-RU" })).Success.Should().BeTrue();
        (await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "+998901234567", Code = "654321", Scope = "two-factor", Culture = "uz" })).Success.Should().BeTrue();

        whatsApp.Sent[0].Template.Should().BeEquivalentTo(new WhatsAppTemplate { Name = "auth_code", LanguageCode = "ru", BodyParameters = ["123456"], ButtonParameter = "123456" });
        whatsApp.Sent[1].Template!.LanguageCode.Should().Be("en_US");
    }

    [Fact]
    public async Task TelegramGatewayChannel_ShouldPostTheCodeWithItsTtl_AndRejectNonNumericCodes()
    {
        var gateway = new ScriptedHandler(_ => Json(HttpStatusCode.OK, """{"ok":true,"result":{"request_id":"r-1"}}"""));
        var services = new ServiceCollection();
        services.AddTelegramGatewayOtpDelivery(o => { o.AccessToken = "gw-token"; o.SenderUsername = "acme"; })
            .ConfigurePrimaryHttpMessageHandler(() => gateway);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IOtpDeliveryHandlerFactory>().Create(OtpChannelNameConstants.TelegramGateway)!;

        var sent = await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "998901234567", Code = "4821", Scope = "two-factor", Lifetime = TimeSpan.FromMinutes(2) });

        sent.Success.Should().BeTrue();
        var request = gateway.Requests.Single();
        request.Path.Should().Be("/sendVerificationMessage");
        request.Authorization.Should().Be("Bearer gw-token");
        var body = System.Text.Json.JsonDocument.Parse(request.Body).RootElement;
        body.GetProperty("phone_number").GetString().Should().Be("+998901234567");
        body.GetProperty("code").GetString().Should().Be("4821");
        body.GetProperty("ttl").GetInt32().Should().Be(120);
        body.GetProperty("sender_username").GetString().Should().Be("acme");

        (await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "+998901234567", Code = "AB3K", Scope = "two-factor" })).FailureReason
            .Should().Be("telegram_gateway_needs_4_to_8_digits");
        gateway.Respond = _ => Json(HttpStatusCode.BadRequest, """{"ok":false,"error":"PHONE_NUMBER_INVALID"}""");
        (await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "+1", Code = "4821", Scope = "two-factor" })).FailureReason
            .Should().Be("telegram_gateway: PHONE_NUMBER_INVALID");
    }

    private static IOtpMessageFormatter Formatter(Action<OtpOptions> configure, Dictionary<string, string?>? host = null)
    {
        var services = new ServiceCollection();
        if (host is not null)
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(host).Build());

        return services.AddOtpService(configure).BuildServiceProvider().GetRequiredService<IOtpMessageFormatter>();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class CapturingEmailBroker : IEmailBroker
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(new EmailSendResult { Success = true });
        }
    }

    private sealed class CapturingWhatsAppBroker : IWhatsAppBroker
    {
        public List<WhatsAppMessage> Sent { get; } = [];

        public Task<WhatsAppSendResult> SendAsync(WhatsAppMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(new WhatsAppSendResult { Success = true, ProviderMessageId = "wamid.1" });
        }
    }

    private sealed class SingleWhatsAppBrokerFactory(IWhatsAppBroker broker) : IWhatsAppBrokerFactory
    {
        public IWhatsAppBroker? Create(string? name = null) => name is null ? broker : null;
    }

    private sealed record RecordedRequest
    {
        public required string Path { get; init; }

        public string? Authorization { get; init; }

        public required string Body { get; init; }
    }

    /// <summary>Answers each request from a script and records what the handler sent.</summary>
    private sealed class ScriptedHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public Func<RecordedRequest, HttpResponseMessage> Respond { get; set; } = respond;

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest
            {
                Path = request.RequestUri!.AbsolutePath,
                Authorization = request.Headers.Authorization?.ToString(),
                Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken),
            };
            Requests.Add(recorded);
            return Respond(recorded);
        }
    }
}
