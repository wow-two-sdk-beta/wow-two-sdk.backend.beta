using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using WoW.Two.Sdk.Backend.Beta.Web.Hosting;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Hosting;

/// <summary>A single-host SPA: route folders answer their own address, other routes fall back to the shell.</summary>
public sealed class SpaHostingTests : IDisposable
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("spa-hosting-").FullName;

    public SpaHostingTests()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "shell");
        Directory.CreateDirectory(Path.Combine(_webRoot, "pricing"));
        File.WriteAllText(Path.Combine(_webRoot, "pricing", "index.html"), "pricing");
    }

    [Theory]
    [InlineData("/pricing", "pricing")]
    [InlineData("/pricing/", "pricing")]
    [InlineData("/", "shell")]
    [InlineData("/app/codes", "shell")]
    public async Task Route_ShouldServeItsOwnDocument_OrTheShell(string path, string expected)
    {
        await using var app = await StartAsync(_ => { });

        using var response = await app.GetTestClient().GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be(expected);
    }

    [Theory]
    [InlineData("/pricing/../../secret")]
    [InlineData("/%2e%2e/secret")]
    public async Task ClimbingPath_ShouldGetTheShell(string path)
    {
        await using var app = await StartAsync(_ => { });

        using var response = await app.GetTestClient().GetAsync(path);

        (await response.Content.ReadAsStringAsync()).Should().Be("shell");
    }

    [Fact]
    public async Task UnmatchedApiRoute_ShouldAnswerJson404_NotTheShell()
    {
        await using var app = await StartAsync(_ => { });

        using var response = await app.GetTestClient().GetAsync("/api/missing");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private async Task<WebApplication> StartAsync(Action<SpaHostingOptions> configure)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { WebRootPath = _webRoot });
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseSpaHosting(configure);
        app.MapSpaFallback(configure);
        await app.StartAsync();
        return app;
    }
}
