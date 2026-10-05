using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WoW.Two.Sdk.Backend.Beta.Ai.Mcp;
using WoW.Two.Sdk.Backend.Beta.Testing.Auth;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Mcp;

/// <summary>Verifies real MCP discovery, invocation and authorization over the HTTP transport.</summary>
public sealed class McpHostTests
{
    [Fact]
    public async Task AnonymousRequest_IsChallenged()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        (await SendAsync(client, "tools/list")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("2025-11-25")]
    [InlineData("2026-07-28")]
    public async Task Tools_HideAndRefuseOperationsWithoutTheScope(string protocol)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Admin", "1");

        using var list = await ReadAsync(await SendAsync(client, "tools/list", protocol: protocol));
        list.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(tool => tool.GetProperty("name").GetString()).Should().BeEquivalentTo("read", "fail");

        using var allowed = await ReadAsync(await SendAsync(client, "tools/call", new { name = "read", arguments = new { } }, protocol));
        allowed.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text")
            .GetString().Should().Be("read-only");

        using var denied = await ReadAsync(await SendAsync(client, "tools/call", new { name = "write", arguments = new { } }, protocol));
        denied.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    [Fact]
    public async Task UnexpectedException_DoesNotExposeSensitiveDetails()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Admin", "1");
        using var result = await ReadAsync(await SendAsync(client, "tools/call", new { name = "fail", arguments = new { } }));
        result.RootElement.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeTrue();
        result.RootElement.ToString().Should().NotContain("sensitive-sentinel");
    }

    [Fact]
    public async Task Initialize_DoesNotAllocateASession()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Admin", "1");
        using var response = await SendAsync(client, "initialize", new
        {
            protocolVersion = "2025-11-25", capabilities = new { }, clientInfo = new { name = "test", version = "1" }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Mcp-Session-Id").Should().BeFalse();
        using var payload = await ReadAsync(response);
        payload.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString()
            .Should().Be("2025-11-25");
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddTestAuth(options => options.RequiredHeader = "X-Test-Admin");
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("mcp", policy => policy.AddAuthenticationSchemes(TestAuthHandler.SchemeName).RequireAuthenticatedUser())
            .AddPolicy("write", policy => policy.RequireClaim("scope", "write"));
        builder.Services.AddStatelessMcpServer().WithTools<SampleTools>();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAuthenticatedMcp("/mcp", "mcp");
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string method, object? parameters = null, string protocol = "2025-11-25")
    {
        var arguments = JsonSerializer.SerializeToNode(parameters ?? new { })!.AsObject();
        if (protocol == "2026-07-28")
            arguments["_meta"] = new JsonObject
            {
                ["io.modelcontextprotocol/protocolVersion"] = protocol,
                ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject(),
                ["io.modelcontextprotocol/clientInfo"] = new JsonObject { ["name"] = "test", ["version"] = "1" }
            };
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = arguments })
        };
        request.Headers.Add("Accept", "application/json, text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", protocol);
        if (protocol == "2026-07-28")
        {
            request.Headers.Add("Mcp-Method", method);
            if (arguments["name"] is { } name)
                request.Headers.Add("Mcp-Name", name.GetValue<string>());
        }
        return client.SendAsync(request);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        var json = body.Split('\n').LastOrDefault(line => line.StartsWith("data: ", StringComparison.Ordinal));
        return JsonDocument.Parse(json is null ? body : json[6..]);
    }

    private sealed class SampleTools
    {
        [McpServerTool(Name = "read", ReadOnly = true)]
        [Authorize]
        public static string Read() => "read-only";

        [McpServerTool(Name = "write")]
        [Authorize(Policy = "write")]
        public static string Write() => throw new InvalidOperationException("Forbidden tool executed.");

        [McpServerTool(Name = "fail", ReadOnly = true)]
        [Authorize]
        public static string Fail() => throw new InvalidOperationException("sensitive-sentinel");
    }
}
