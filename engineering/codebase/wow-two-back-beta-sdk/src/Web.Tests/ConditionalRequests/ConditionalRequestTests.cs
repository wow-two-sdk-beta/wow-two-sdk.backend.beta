using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Meta;
using WoW.Two.Sdk.Backend.Beta.Web.ConditionalRequests;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ConditionalRequests;

/// <summary>Conditional GETs through an <c>AddApiDefaults</c> host: off by default, tags and 304s once configured.</summary>
public sealed class ConditionalRequestTests
{
    private static readonly string[] Orders = ["order-1", "order-2"];

    [Fact]
    public async Task WithoutConfiguration_ResponsesCarryNoTag()
    {
        await using var app = await StartAsync(enabled: false);

        using var response = await app.GetTestServer().CreateClient().GetAsync("/orders");

        response.Headers.ETag.Should().BeNull();
        (await response.Content.ReadAsStringAsync()).Should().Contain("order-1");
    }

    [Fact]
    public async Task Enabled_TagsTheBodyAndAnswersAMatchingRevalidationWith304()
    {
        await using var app = await StartAsync(enabled: true);
        var client = app.GetTestServer().CreateClient();

        using var first = await client.GetAsync("/orders");
        var tag = first.Headers.ETag;
        tag.Should().NotBeNull();
        tag!.IsWeak.Should().BeTrue();

        using var revalidation = new HttpRequestMessage(HttpMethod.Get, "/orders");
        revalidation.Headers.IfNoneMatch.Add(tag);
        using var notModified = await client.SendAsync(revalidation);
        notModified.StatusCode.Should().Be(HttpStatusCode.NotModified);
        (await notModified.Content.ReadAsByteArrayAsync()).Should().BeEmpty();

        using var stale = new HttpRequestMessage(HttpMethod.Get, "/orders");
        stale.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"other\"", isWeak: true));
        (await client.SendAsync(stale)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Enabled_HonoursAnEndpointTagAndSkipsStreamsAndOversizedBodies()
    {
        await using var app = await StartAsync(enabled: true, maxBufferBytes: 1024);
        var client = app.GetTestServer().CreateClient();

        using var versioned = await client.GetAsync("/orders/1");
        versioned.Headers.ETag!.Tag.Should().Be("\"42\"");
        using var revalidation = new HttpRequestMessage(HttpMethod.Get, "/orders/1");
        revalidation.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"42\""));
        (await client.SendAsync(revalidation)).StatusCode.Should().Be(HttpStatusCode.NotModified);

        using var large = await client.GetAsync("/large");
        large.Headers.ETag.Should().BeNull();
        (await large.Content.ReadAsStringAsync()).Should().HaveLength(4096);

        using var events = await client.GetAsync("/events");
        events.Headers.ETag.Should().BeNull();
        (await events.Content.ReadAsStringAsync()).Should().Be("data: hi\n\n");
    }

    [Fact]
    public void FailsIfMatch_RefusesOnlyAStaleVersion()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.IfMatch = "\"41\"";
        context.Request.FailsIfMatch(42).Should().BeTrue();
        context.Request.FailsIfMatch(41).Should().BeFalse();

        context.Request.Headers.IfMatch = "*";
        context.Request.FailsIfMatch(42).Should().BeFalse();
        new DefaultHttpContext().Request.FailsIfMatch(42).Should().BeFalse();
    }

    private static async Task<WebApplication> StartAsync(bool enabled, int maxBufferBytes = 1024 * 1024)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConditionalRequests:Enabled"] = enabled ? "true" : "false",
            ["ConditionalRequests:MaxBufferBytes"] = maxBufferBytes.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
        });
        var app = builder.Build();
        app.UseApiDefaults();
        app.MapGet("/orders", () => Results.Json(Orders));
        app.MapGet("/orders/1", (HttpContext http) =>
        {
            http.Response.SetEntityTag(42);
            return Results.Json(new { id = 1 });
        });
        app.MapGet("/large", () => Results.Text(new string('x', 4096)));
        app.MapGet("/events", async (HttpContext http) =>
        {
            http.Response.ContentType = "text/event-stream";
            await http.Response.WriteAsync("data: hi\n\n");
        });
        await app.StartAsync();
        return app;
    }
}
