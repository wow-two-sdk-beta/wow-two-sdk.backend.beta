using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Web.SecureHeaders;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.SecureHeaders;

/// <summary>
/// Covers the cross-origin opt-outs on <c>UseOwaspSecureHeaders</c> over an in-memory TestServer host: the
/// unconfigured call emits the opener and embedder policies, and either flag switched off drops that header
/// while the rest of the preset stays.
/// </summary>
public sealed class SecureHeadersTests
{
    private const string OpenerPolicyHeader = "Cross-Origin-Opener-Policy";

    private const string EmbedderPolicyHeader = "Cross-Origin-Embedder-Policy";

    private const string ResourcePolicyHeader = "Cross-Origin-Resource-Policy";

    /// <summary>Boots an in-memory TestServer host whose only middleware is the preset under <paramref name="configure"/>.</summary>
    private static async Task<WebApplication> StartHeaderHostAsync(Action<SecureHeadersOptions>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var app = builder.Build();

        app.UseOwaspSecureHeaders(configure);
        app.MapGet("/", () => "ok");

        await app.StartAsync();

        return app;
    }

    private static async Task<HttpResponseMessage> GetRootAsync(WebApplication app)
    {
        using var client = app.GetTestClient();

        return await client.GetAsync(new Uri("/", UriKind.Relative));
    }

    [Fact]
    public async Task UseOwaspSecureHeaders_ShouldEmitBothCrossOriginPolicies_WhenUnconfigured()
    {
        await using var app = await StartHeaderHostAsync();

        using var response = await GetRootAsync(app);

        response.Headers.GetValues(OpenerPolicyHeader).Should().ContainSingle().Which.Should().Be("same-origin");
        response.Headers.GetValues(EmbedderPolicyHeader).Should().ContainSingle().Which.Should().Be("require-corp");
    }

    [Fact]
    public async Task UseOwaspSecureHeaders_ShouldDropOpenerPolicyOnly_WhenOpenerOptedOut()
    {
        await using var app = await StartHeaderHostAsync(options => options.EnableCrossOriginOpenerPolicy = false);

        using var response = await GetRootAsync(app);

        response.Headers.Contains(OpenerPolicyHeader).Should().BeFalse();
        response.Headers.GetValues(EmbedderPolicyHeader).Should().ContainSingle().Which.Should().Be("require-corp");
    }

    [Fact]
    public async Task UseOwaspSecureHeaders_ShouldDropEmbedderPolicyOnly_WhenEmbedderOptedOut()
    {
        await using var app = await StartHeaderHostAsync(options => options.EnableCrossOriginEmbedderPolicy = false);

        using var response = await GetRootAsync(app);

        response.Headers.Contains(EmbedderPolicyHeader).Should().BeFalse();
        response.Headers.GetValues(OpenerPolicyHeader).Should().ContainSingle().Which.Should().Be("same-origin");
    }

    [Fact]
    public async Task UseOwaspSecureHeaders_ShouldKeepTheRestOfThePreset_WhenBothOptedOut()
    {
        await using var app = await StartHeaderHostAsync(options =>
        {
            options.EnableCrossOriginOpenerPolicy = false;
            options.EnableCrossOriginEmbedderPolicy = false;
        });

        using var response = await GetRootAsync(app);

        response.Headers.Contains(OpenerPolicyHeader).Should().BeFalse();
        response.Headers.Contains(EmbedderPolicyHeader).Should().BeFalse();
        response.Headers.GetValues(ResourcePolicyHeader).Should().ContainSingle().Which.Should().Be("same-origin");
        response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
    }
}
