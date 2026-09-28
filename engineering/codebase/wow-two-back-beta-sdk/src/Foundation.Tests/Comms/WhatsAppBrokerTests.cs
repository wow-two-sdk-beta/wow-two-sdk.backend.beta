using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp;
using WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Meta;
using WoW.Two.Sdk.Backend.Beta.Comms.WhatsApp.Twilio;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Comms;

/// <summary>WhatsApp brokers against scripted provider responses, and brokers chosen by host configuration.</summary>
public sealed class WhatsAppBrokerTests
{
    private static readonly WhatsAppMessage CodeMessage = new()
    {
        To = "+998901234567",
        Template = new WhatsAppTemplate { Name = "auth_code", LanguageCode = "ru", BodyParameters = ["123456"], ButtonParameter = "123456" },
    };

    [Fact]
    public async Task Meta_ShouldPostAnAuthenticationTemplateAndReturnTheMessageId()
    {
        var provider = new ScriptedHandler(_ => Json(HttpStatusCode.OK, """{"messaging_product":"whatsapp","contacts":[{"wa_id":"998901234567"}],"messages":[{"id":"wamid.HBg1"}]}"""));
        var broker = Build(services => services.AddMetaWhatsAppBroker(o => { o.AccessToken = "token"; o.PhoneNumberId = "PN1"; }), provider);

        var result = await broker.SendAsync(CodeMessage);

        result.Should().BeEquivalentTo(new WhatsAppSendResult { Success = true, ProviderMessageId = "wamid.HBg1" });
        var request = provider.Requests.Single();
        request.Path.Should().Be("/v23.0/PN1/messages");
        request.Authorization.Should().Be("Bearer token");
        var body = JsonDocument.Parse(request.Body).RootElement;
        body.GetProperty("to").GetString().Should().Be("998901234567");
        var template = body.GetProperty("template");
        template.GetProperty("name").GetString().Should().Be("auth_code");
        template.GetProperty("language").GetProperty("code").GetString().Should().Be("ru");
        var components = template.GetProperty("components");
        components[0].GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be("123456");
        components[1].GetProperty("sub_type").GetString().Should().Be("url");
        components[1].GetProperty("index").GetString().Should().Be("0");
        components[1].GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be("123456");
    }

    [Fact]
    public async Task Meta_ShouldSendFreeTextAndReportTheGraphError()
    {
        var provider = new ScriptedHandler(_ => Json(HttpStatusCode.BadRequest, """{"error":{"message":"Re-engagement message","type":"OAuthException","code":131047}}"""));
        var broker = Build(services => services.AddMetaWhatsAppBroker(o => { o.AccessToken = "token"; o.PhoneNumberId = "PN1"; }), provider);

        var result = await broker.SendAsync(new WhatsAppMessage { To = "+998901234567", Text = "Hi" });

        result.FailureReason.Should().Be("meta_131047: Re-engagement message");
        var body = JsonDocument.Parse(provider.Requests.Single().Body).RootElement;
        body.GetProperty("type").GetString().Should().Be("text");
        body.GetProperty("text").GetProperty("body").GetString().Should().Be("Hi");
        (await broker.SendAsync(new WhatsAppMessage { To = "+998901234567" })).FailureReason.Should().Be("empty_message");
    }

    [Fact]
    public async Task Twilio_ShouldPostTheContentSidWithNumberedVariables()
    {
        var provider = new ScriptedHandler(_ => Json(HttpStatusCode.Created, """{"sid":"SM9","status":"queued"}"""));
        var broker = Build(services => services.AddTwilioWhatsAppBroker(o => { o.AccountSid = "AC1"; o.AuthToken = "secret"; o.From = "+15550001111"; }), provider);

        (await broker.SendAsync(CodeMessage with { Template = CodeMessage.Template! with { Name = "HX123" } })).ProviderMessageId.Should().Be("SM9");

        var form = provider.Requests.Single().Body;
        form.Should().Contain("To=whatsapp%3A%2B998901234567").And.Contain("From=whatsapp%3A%2B15550001111").And.Contain("ContentSid=HX123");
        Uri.UnescapeDataString(form).Should().Contain("""ContentVariables={"1":"123456"}""");
    }

    [Fact]
    public void HostConfiguration_ShouldDecideWhichBrokersExistAndWhichIsTheDefault()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Comms:WhatsApp:DefaultBroker"] = "twilio",
            ["Comms:WhatsApp:Meta:AccessToken"] = "token",
            ["Comms:WhatsApp:Meta:PhoneNumberId"] = "PN1",
            ["Comms:WhatsApp:Twilio:AccountSid"] = "AC1",
            ["Comms:WhatsApp:Twilio:AuthToken"] = "secret",
            ["Comms:WhatsApp:Twilio:From"] = "+15550001111",
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        services.AddWhatsAppBrokers(configuration);
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IWhatsAppBrokerFactory>();

        factory.Create("meta").Should().BeOfType<MetaWhatsAppBroker>();
        factory.Create("TWILIO").Should().BeOfType<TwilioWhatsAppBroker>();
        factory.Create().Should().BeOfType<TwilioWhatsAppBroker>();
        factory.Create("viber").Should().BeNull();
        provider.GetRequiredService<MetaWhatsAppOptions>().PhoneNumberId.Should().Be("PN1");
    }

    [Fact]
    public void HostConfiguration_WithoutASection_ShouldRegisterNoBroker()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection().AddWhatsAppBrokers(configuration);
        using var provider = services.BuildServiceProvider();

        provider.GetService<IWhatsAppBroker>().Should().BeNull();
    }

    private static IWhatsAppBroker Build(Action<IServiceCollection> register, ScriptedHandler provider)
    {
        var services = new ServiceCollection();
        register(services);
        foreach (var name in new[] { MetaWhatsAppBroker.HttpClientName, TwilioWhatsAppBroker.HttpClientName })
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => provider);

        return services.BuildServiceProvider().GetRequiredService<IWhatsAppBroker>();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed record RecordedRequest
    {
        public required string Path { get; init; }

        public string? Authorization { get; init; }

        public required string Body { get; init; }
    }

    /// <summary>Answers each request from a script and records what the broker sent.</summary>
    private sealed class ScriptedHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
    {
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
            return respond(recorded);
        }
    }
}
