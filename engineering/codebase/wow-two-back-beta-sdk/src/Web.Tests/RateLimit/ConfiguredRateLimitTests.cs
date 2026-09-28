using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.RateLimit;

/// <summary>Rate-limit policies declared in configuration: named, global, and rejected as problem details.</summary>
public sealed class ConfiguredRateLimitTests
{
    [Fact]
    public async Task NamedPolicy_LimitsOnlyTheEndpointsThatOptIn()
    {
        await using var app = await StartAsync(new Dictionary<string, string?>
        {
            ["RateLimits:Policies:login:PermitLimit"] = "2",
            ["RateLimits:Policies:login:Window"] = "00:01:00",
            ["RateLimits:Policies:login:Algorithm"] = "FixedWindow",
        });
        var client = app.GetTestServer().CreateClient();

        (await client.PostAsync("/login", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/login", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var rejected = await client.PostAsync("/login", null);

        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.Should().NotBeNull();
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().Should().Be("TooManyRequests");
        for (var attempt = 0; attempt < 5; attempt++)
            (await client.GetAsync("/open")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GlobalPolicy_LimitsEveryRequest()
    {
        await using var app = await StartAsync(new Dictionary<string, string?>
        {
            ["RateLimits:Policies:everyone:PermitLimit"] = "1",
            ["RateLimits:Policies:everyone:Algorithm"] = "FixedWindow",
            ["RateLimits:GlobalPolicy"] = "everyone",
        });
        var client = app.GetTestServer().CreateClient();

        (await client.GetAsync("/open")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/open")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UnknownGlobalPolicy_FailsAtStartup()
    {
        var act = () => StartAsync(new Dictionary<string, string?> { ["RateLimits:GlobalPolicy"] = "missing" });

        await act.Should().ThrowAsync<OptionsValidationException>().WithMessage("*missing*");
    }

    [Fact]
    public async Task Policies_BindLazily_WhenConfigurationIsAddedAfterRegistration()
    {
        // A test host's overrides land after the application registered its services; the limiter still sees them.
        await using var app = await StartAsync(
            new Dictionary<string, string?> { ["RateLimits:Policies:login:PermitLimit"] = "1" },
            new Dictionary<string, string?> { ["RateLimits:Policies:login:PermitLimit"] = "3" });
        var client = app.GetTestServer().CreateClient();

        for (var attempt = 0; attempt < 3; attempt++)
            (await client.PostAsync("/login", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync("/login", null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    private static async Task<WebApplication> StartAsync(
        Dictionary<string, string?> settings, Dictionary<string, string?>? addedLater = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
        });
        if (addedLater is not null) builder.Configuration.AddInMemoryCollection(addedLater);
        var app = builder.Build();
        app.UseApiDefaults();
        app.MapPost("/login", () => "ok").RequireRateLimiting("login");
        app.MapGet("/open", () => "ok");
        await app.StartAsync();
        return app;
    }
}
