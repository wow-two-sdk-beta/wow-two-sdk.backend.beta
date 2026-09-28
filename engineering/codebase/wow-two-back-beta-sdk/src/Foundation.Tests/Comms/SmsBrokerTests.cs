using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms.Eskiz;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms.Twilio;
using WoW.Two.Sdk.Backend.Beta.Comms.Sms.Vonage;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Models;
using WoW.Two.Sdk.Backend.Beta.Identity.Otp.Sms;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Foundation.Tests.Comms;

/// <summary>SMS brokers against scripted provider responses: request shape, success mapping, failures and Eskiz re-sign-in.</summary>
public sealed class SmsBrokerTests
{
    private static readonly SmsMessage Message = new() { To = "+998901234567", Body = "Hello" };

    [Fact]
    public async Task Twilio_ShouldPostFormWithBasicAuthAndReturnTheSid()
    {
        var provider = new ScriptedProvider(_ => Json(HttpStatusCode.Created, """{"sid":"SM123","status":"queued"}"""));
        var broker = Build(services => services.AddTwilioSmsBroker(o => { o.AccountSid = "AC1"; o.AuthToken = "secret"; }), provider, from: "+15550001111");

        var result = await broker.SendAsync(Message);

        result.Should().BeEquivalentTo(new SmsSendResult { Success = true, ProviderMessageId = "SM123" });
        var request = provider.Requests.Single();
        request.Path.Should().Be("/2010-04-01/Accounts/AC1/Messages.json");
        request.Authorization.Should().Be("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("AC1:secret")));
        request.Body.Should().Contain("To=%2B998901234567").And.Contain("From=%2B15550001111").And.Contain("Body=Hello");
    }

    [Fact]
    public async Task Twilio_ShouldReportTheProviderError()
    {
        var provider = new ScriptedProvider(_ => Json(HttpStatusCode.BadRequest, """{"code":21211,"message":"Invalid 'To' Phone Number"}"""));
        var broker = Build(services => services.AddTwilioSmsBroker(o => { o.AccountSid = "AC1"; o.AuthToken = "secret"; o.MessagingServiceSid = "MG1"; }), provider);

        var result = await broker.SendAsync(Message);

        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("twilio_21211: Invalid 'To' Phone Number");
        provider.Requests.Single().Body.Should().Contain("MessagingServiceSid=MG1");
    }

    [Fact]
    public async Task Vonage_ShouldSendDigitsAndMapPartStatuses()
    {
        var provider = new ScriptedProvider(_ => Json(HttpStatusCode.OK, """{"message-count":"1","messages":[{"status":"0","message-id":"V-1"}]}"""));
        var broker = Build(services => services.AddVonageSmsBroker(o => { o.ApiKey = "k"; o.ApiSecret = "s"; }), provider, from: "Acme");

        (await broker.SendAsync(Message)).ProviderMessageId.Should().Be("V-1");
        provider.Requests.Single().Body.Should().Contain("to=998901234567").And.Contain("from=Acme");

        provider.Respond = _ => Json(HttpStatusCode.OK, """{"messages":[{"status":"4","error-text":"Bad Credentials"}]}""");
        (await broker.SendAsync(Message)).FailureReason.Should().Be("vonage_4: Bad Credentials");
    }

    [Fact]
    public async Task Eskiz_ShouldSignInOnceAndAgainAfterA401()
    {
        var logins = 0;
        var sends = 0;
        var provider = new ScriptedProvider(request => request.Path switch
        {
            "/api/auth/login" => Json(HttpStatusCode.OK, $$"""{"message":"token_generated","data":{"token":"t{{++logins}}"},"token_type":"bearer"}"""),
            "/api/message/sms/send" when request.Authorization == "Bearer t1" && ++sends > 1 => new HttpResponseMessage(HttpStatusCode.Unauthorized),
            "/api/message/sms/send" => Json(HttpStatusCode.OK, """{"id":"59bf10a2","message":"Waiting for SMS provider","status":"waiting"}"""),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var broker = Build(services => services.AddEskizSmsBroker(o => { o.Email = "a@b.uz"; o.Password = "p"; }), provider);

        (await broker.SendAsync(Message)).ProviderMessageId.Should().Be("59bf10a2");
        (await broker.SendAsync(Message)).Success.Should().BeTrue();

        logins.Should().Be(2);
        provider.Requests.Where(r => r.Path == "/api/message/sms/send").Should()
            .AllSatisfy(r => r.Body.Should().Contain("mobile_phone=998901234567").And.Contain("from=4546"));
    }

    [Fact]
    public async Task SmsOtpDelivery_ShouldFormatTheTemplateAndForwardTheBrokerOutcome()
    {
        var provider = new ScriptedProvider(_ => Json(HttpStatusCode.OK, """{"messages":[{"status":"0","message-id":"V-9"}]}"""));
        var services = new ServiceCollection();
        services.AddOtpService();
        services.AddSmsOtpDelivery(o => o.ScopeDisplayNames["login"] = "Acme");
        services.AddVonageSmsBroker(o => { o.ApiKey = "k"; o.ApiSecret = "s"; }).ConfigurePrimaryHttpMessageHandler(() => provider);
        services.AddSmsDefaults(o => o.DefaultFrom = "Acme");
        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetServices<IOtpDeliveryHandler>().OfType<SmsOtpDeliveryHandler>().Single();

        var result = await handler.SendAsync(new OtpDeliveryEnvelopeModel { DeliveryAddress = "+998901234567", Code = "123456", Scope = "login" });

        result.Success.Should().BeTrue();
        provider.Requests.Single().Body.Should().Contain(Uri.EscapeDataString("123456 is your Acme code. It expires in 5 minutes.").Replace("%20", "+", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HostConfiguration_ShouldDecideTheBrokers_AndOtpDeliveryShouldFollowTheNamedOne()
    {
        var provider = new ScriptedProvider(request => request.Path switch
        {
            "/api/auth/login" => Json(HttpStatusCode.OK, """{"data":{"token":"t1"}}"""),
            "/api/message/sms/send" => Json(HttpStatusCode.OK, """{"id":"E-1","status":"waiting"}"""),
            _ => Json(HttpStatusCode.Created, """{"sid":"SM1"}"""),
        });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Comms:Sms:DefaultFrom"] = "Acme",
            ["Comms:Sms:DefaultBroker"] = "eskiz",
            ["Comms:Sms:Twilio:AccountSid"] = "AC1",
            ["Comms:Sms:Twilio:AuthToken"] = "secret",
            ["Comms:Sms:Eskiz:Email"] = "a@b.uz",
            ["Comms:Sms:Eskiz:Password"] = "p",
            ["Identity:Otp:Sms:Broker"] = "twilio",
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        services.AddSmsBrokers(configuration);
        services.AddSmsOtpDelivery();
        foreach (var name in new[] { TwilioSmsBroker.HttpClientName, EskizSmsBroker.HttpClientName })
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => provider);
        await using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<ISmsBrokerFactory>();

        factory.Create().Should().BeOfType<EskizSmsBroker>();
        factory.Create("twilio").Should().BeOfType<TwilioSmsBroker>();
        factory.Create("vonage").Should().BeNull();

        await using var scope = serviceProvider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredKeyedService<IOtpDeliveryHandler>(OtpChannelNameConstants.Sms);
        var envelope = new OtpDeliveryEnvelopeModel { DeliveryAddress = "+998901234567", Code = "123456", Scope = "login", Text = "Code 123456" };
        (await handler.SendAsync(envelope)).Success.Should().BeTrue();
        (await handler.SendAsync(envelope with { Broker = "eskiz" })).Success.Should().BeTrue();
        (await handler.SendAsync(envelope with { Broker = "vonage" })).FailureReason.Should().Be("sms_broker_not_registered: vonage");

        provider.Requests.Should().HaveCount(3);
        provider.Requests[0].Path.Should().Be("/2010-04-01/Accounts/AC1/Messages.json");
        provider.Requests[0].Body.Should().Contain("Body=Code+123456").And.Contain("From=Acme");
        provider.Requests[2].Path.Should().Be("/api/message/sms/send");
    }

    private static ISmsBroker Build(Action<IServiceCollection> register, ScriptedProvider provider, string? from = null)
    {
        var services = new ServiceCollection();
        register(services);
        if (from is not null)
            services.AddSmsDefaults(o => o.DefaultFrom = from);
        foreach (var name in new[] { TwilioSmsBroker.HttpClientName, VonageSmsBroker.HttpClientName, EskizSmsBroker.HttpClientName })
            services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => provider);

        return services.BuildServiceProvider().GetRequiredService<ISmsBroker>();
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
    private sealed class ScriptedProvider(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
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
