using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Meta;
using WoW.Two.Sdk.Backend.Beta.Web.Captcha;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Captcha;

/// <summary>Captcha-gated endpoints against a scripted siteverify provider, switched on by host configuration.</summary>
public sealed class CaptchaTests
{
    [Fact]
    public async Task Disabled_ShouldPassWithoutAToken()
    {
        await using var host = await StartAsync(enabled: false);

        (await host.Client.PostAsJsonAsync("/signup", new { email = "a@b.test" })).StatusCode.Should().Be(HttpStatusCode.OK);
        host.Provider.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Enabled_ShouldAskTheProviderWithTheHeaderTokenSecretAndAddress()
    {
        await using var host = await StartAsync();
        host.Provider.Answer = """{"success":true,"hostname":"app.test","action":"signup","challenge_ts":"2026-09-28T09:00:00Z"}""";

        (await host.PostAsync(token: "tok-1")).StatusCode.Should().Be(HttpStatusCode.OK);

        var call = host.Provider.Calls.Single();
        call.Uri.Should().Be(new Uri("https://challenges.cloudflare.com/turnstile/v0/siteverify"));
        call.Form.Should().Contain("secret=site-secret").And.Contain("response=tok-1");
        host.Handled.Should().Be(1);
    }

    [Fact]
    public async Task FormField_ShouldCarryTheToken()
    {
        await using var host = await StartAsync(provider: "hcaptcha");
        host.Provider.Answer = """{"success":true,"hostname":"app.test"}""";

        using var form = new FormUrlEncodedContent([new("h-captcha-response", "tok-form"), new("email", "a@b.test")]);
        (await host.Client.PostAsync("/contact", form)).StatusCode.Should().Be(HttpStatusCode.OK);
        host.Provider.Calls.Single().Uri.Should().Be(new Uri("https://api.hcaptcha.com/siteverify"));
        host.Provider.Calls.Single().Form.Should().Contain("response=tok-form");
    }

    [Theory]
    [InlineData(null, """{"success":true,"hostname":"app.test"}""")]
    [InlineData("tok", """{"success":false,"error-codes":["timeout-or-duplicate"]}""")]
    [InlineData("tok", """{"success":true,"hostname":"evil.test","action":"signup"}""")]
    [InlineData("tok", """{"success":true,"hostname":"app.test","action":"login"}""")]
    [InlineData("tok", """{"success":true,"hostname":"app.test","action":"signup","score":0.2}""")]
    public async Task FailingTokens_ShouldAnswer400BeforeTheHandler(string? token, string answer)
    {
        await using var host = await StartAsync(provider: "recaptcha");
        host.Provider.Answer = answer;

        using var response = await host.PostAsync(token);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Complete the captcha check");
        host.Handled.Should().Be(0);
    }

    [Fact]
    public async Task ProviderOutage_ShouldAnswer503_UnlessItFailsOpen()
    {
        await using (var closed = await StartAsync())
        {
            closed.Provider.Status = HttpStatusCode.BadGateway;
            (await closed.PostAsync("tok")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        }

        await using var open = await StartAsync(failOpen: true);
        open.Provider.Status = HttpStatusCode.BadGateway;
        (await open.PostAsync("tok")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<CaptchaHost> StartAsync(bool enabled = true, string provider = "turnstile", bool failOpen = false)
    {
        var scripted = new ScriptedProvider();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Web:Captcha:Enabled"] = enabled ? "true" : "false",
            ["Web:Captcha:Provider"] = provider,
            ["Web:Captcha:Secret"] = "site-secret",
            ["Web:Captcha:ExpectedHostnames:0"] = "app.test",
            ["Web:Captcha:FailOpen"] = failOpen ? "true" : "false",
        });
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
            options.EnableRateLimiting = false;
        });
        builder.Services.AddCaptcha();
        builder.Services.AddHttpClient(SiteVerifyCaptchaBroker.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => scripted);
        var app = builder.Build();
        app.UseApiDefaults();
        var host = new CaptchaHost(app, scripted);
        app.MapPost("/signup", () =>
        {
            host.Handled++;
            return Results.Ok();
        }).RequireCaptcha("signup");
        app.MapPost("/contact", ([Microsoft.AspNetCore.Mvc.FromForm] string email) => Results.Ok(email)).RequireCaptcha().DisableAntiforgery();
        await app.StartAsync();
        return host;
    }

    private sealed class CaptchaHost(WebApplication app, ScriptedProvider provider) : IAsyncDisposable
    {
        public ScriptedProvider Provider { get; } = provider;

        private HttpClient? _client;

        public HttpClient Client => _client ??= app.GetTestServer().CreateClient();

        public int Handled { get; set; }

        public async Task<HttpResponseMessage> PostAsync(string? token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/signup") { Content = JsonContent.Create(new { email = "a@b.test" }) };
            if (token is not null)
                request.Headers.Add("X-Captcha-Token", token);

            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();
            await app.DisposeAsync();
        }
    }

    /// <summary>Answers siteverify posts with a scripted status and body, recording each call.</summary>
    private sealed class ScriptedProvider : HttpMessageHandler
    {
        public List<(Uri Uri, string Form)> Calls { get; } = [];

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public string Answer { get; set; } = """{"success":true}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(Status) { Content = new StringContent(Answer, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
