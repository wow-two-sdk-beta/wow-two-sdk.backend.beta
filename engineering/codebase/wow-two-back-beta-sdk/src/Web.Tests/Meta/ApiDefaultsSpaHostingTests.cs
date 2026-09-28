using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Meta;

/// <summary>An app bundle served through the API defaults carries the secure headers and the response encoding.</summary>
public sealed class ApiDefaultsSpaHostingTests : IAsyncLifetime
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("api-defaults-spa-").FullName;
    private WebApplication _app = null!;

    public async Task InitializeAsync()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>shell</title>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app-3f9a.js"), new string('x', 4096));

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { WebRootPath = _webRoot });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
            options.SpaHosting = _ => { };
        });
        _app = builder.Build();
        _app.UseApiDefaults();
        _app.MapGet("/api/ping", () => "pong");
        await _app.StartAsync();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/app/codes")]
    [InlineData("/assets/app-3f9a.js")]
    public async Task Bundle_ShouldCarryTheSecureHeaders(string path)
    {
        using var response = await _app.GetTestClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Fact]
    public async Task Script_ShouldBeCompressed_AndCachedAsImmutable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/assets/app-3f9a.js");
        request.Headers.AcceptEncoding.ParseAdd("gzip");

        using var response = await _app.GetTestClient().SendAsync(request);

        response.Content.Headers.ContentEncoding.Should().ContainSingle().Which.Should().Be("gzip");
        response.Headers.CacheControl!.ToString().Should().Be("public, max-age=31536000, immutable");
    }

    [Fact]
    public async Task Endpoints_ShouldStillWin_OverTheFallback()
    {
        var client = _app.GetTestClient();

        (await client.GetStringAsync("/api/ping")).Should().Be("pong");
        (await client.GetAsync("/api/missing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        Directory.Delete(_webRoot, recursive: true);
    }
}
