using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Testing.Web;
using WoW.Two.Sdk.Backend.Beta.Web.Antiforgery;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Antiforgery;

/// <summary>Cookie-carrying unsafe requests need the echoed token; cookieless and exempt requests pass.</summary>
public sealed class SpaAntiforgeryTests
{
    [Fact]
    public async Task SafeRequest_IssuesAReadableTokenCookie()
    {
        await using var app = await StartAsync();

        using var response = await app.GetTestClient().GetAsync("/api/codes");

        Cookies(response).Should().Contain(cookie => cookie.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal)
            && !cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UnsafeRequest_WithCookiesButNoToken_IsRejected()
    {
        await using var app = await StartAsync();
        var (cookieHeader, _) = await IssueAsync(app);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/codes");
        request.Headers.Add("Cookie", cookieHeader);
        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("AntiforgeryValidationFailed");
    }

    [Fact]
    public async Task UnsafeRequest_WithTheEchoedToken_Passes()
    {
        await using var app = await StartAsync();
        var (cookieHeader, token) = await IssueAsync(app);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/codes");
        request.Headers.Add("Cookie", cookieHeader);
        request.Headers.Add("X-XSRF-TOKEN", token);
        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/codes", false)]
    [InlineData("/api/billing/webhook", true)]
    public async Task UnsafeRequest_WithoutCookiesOrOnAnExemptPath_Passes(string path, bool sendCookies)
    {
        await using var app = await StartAsync();
        var (cookieHeader, _) = await IssueAsync(app);

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (sendCookies)
            request.Headers.Add("Cookie", cookieHeader);
        using var response = await app.GetTestClient().SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnsafeRequest_ThroughTheTestingHandler_Passes()
    {
        await using var app = await StartAsync();
        var server = app.GetTestServer();
        using var client = new HttpClient(new SpaAntiforgeryHandler("/api/codes") { InnerHandler = server.CreateHandler() })
        {
            BaseAddress = server.BaseAddress,
        };
        client.DefaultRequestHeaders.Add("Cookie", "session=signed-in");

        using var response = await client.PostAsync("/api/codes", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSpaAntiforgery(options => options.ExemptPathPrefixes.Add("/api/billing/webhook"));
        var app = builder.Build();
        app.UseSpaAntiforgery();
        app.MapGet("/api/codes", () => "list");
        app.MapPost("/api/codes", () => "created");
        app.MapPost("/api/billing/webhook", () => "received");
        await app.StartAsync();
        return app;
    }

    private static async Task<(string CookieHeader, string Token)> IssueAsync(WebApplication app)
    {
        using var response = await app.GetTestClient().GetAsync("/api/codes");
        string[] pairs = [.. Cookies(response).Select(cookie => cookie.Split(';')[0])];
        string token = pairs.Single(pair => pair.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal))["XSRF-TOKEN=".Length..];
        return (string.Join("; ", pairs), Uri.UnescapeDataString(token));
    }

    private static IEnumerable<string> Cookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
}
