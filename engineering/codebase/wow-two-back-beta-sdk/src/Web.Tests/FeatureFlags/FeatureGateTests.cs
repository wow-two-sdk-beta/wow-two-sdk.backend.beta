using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using WoW.Two.Sdk.Backend.Beta.FeatureFlags.Core;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Mediator.FeatureGates;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.FeatureFlags;

/// <summary>Feature gates: a disabled flag hides an endpoint or a mediator request as not found.</summary>
public sealed class FeatureGateTests
{
    [Fact]
    public async Task Endpoint_ShouldAnswer404UntilEveryFeatureIsEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FeatureManagement:Checkout"] = "true",
            ["FeatureManagement:NewPricing"] = "false",
        });
        builder.Services.AddFeatureFlags();
        await using var app = builder.Build();
        app.MapGet("/checkout", () => "ok").RequireFeatures("Checkout");
        app.MapGet("/pricing", () => "ok").RequireFeatures("Checkout", "NewPricing");
        await app.StartAsync();
        var client = app.GetTestServer().CreateClient();

        (await client.GetAsync("/checkout")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/pricing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Interceptor_ShouldRefuseAGatedRequestWhileItsFeatureIsOff()
    {
        var flags = new FixedFlags { ["Beta"] = false, ["Live"] = true };
        var nextRan = 0;
        ValueTask<string> Next()
        {
            nextRan++;
            return ValueTask.FromResult("ran");
        }

        var dark = () => new FeatureGatingInterceptor<GatedRequest, string>(flags).HandleAsync(new GatedRequest(["Live", "Beta"]), Next, default).AsTask();
        (await dark.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.NotFound);

        (await new FeatureGatingInterceptor<GatedRequest, string>(flags).HandleAsync(new GatedRequest(["Live"]), Next, default)).Should().Be("ran");
        (await new FeatureGatingInterceptor<string, string>(flags).HandleAsync("plain", Next, default)).Should().Be("ran");
        nextRan.Should().Be(2);
    }

    private sealed class GatedRequest(IReadOnlyList<string> features) : IFeatureGated
    {
        public IReadOnlyList<string> RequiredFeatures { get; } = features;
    }

    private sealed class FixedFlags : Dictionary<string, bool>, IFeatureFlags
    {
        public ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TryGetValue(feature, out var enabled) && enabled);
    }
}
