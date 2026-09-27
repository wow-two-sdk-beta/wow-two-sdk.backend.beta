using System.Net;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Web.Hosting;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Hosting;

/// <summary>Forwarded headers apply only from trusted proxies; probe hosts pass a restricted host allowlist.</summary>
public sealed class ProxyAwareHostingTests
{
    private const string SocketHeader = "X-Test-Socket-Address";

    [Theory]
    [InlineData("203.0.113.9", "203.0.113.9")]
    [InlineData("127.0.0.1", "198.51.100.7")]
    public async Task ForwardedFor_AppliesOnlyFromLoopbackByDefault(string socket, string expectedClient)
    {
        await using var app = await StartAsync(_ => { });

        (await ClientAddressAsync(app, socket)).Should().Be(expectedClient);
    }

    [Fact]
    public async Task ForwardedFor_AppliesFromAConfiguredProxyOrNetwork()
    {
        await using var byProxy = await StartAsync(options => options.TrustedProxies.Add("10.0.0.5"));
        await using var byNetwork = await StartAsync(options => options.TrustedNetworks.Add("172.16.0.0/12"));

        (await ClientAddressAsync(byProxy, "10.0.0.5")).Should().Be("198.51.100.7");
        (await ClientAddressAsync(byProxy, "10.0.0.6")).Should().Be("10.0.0.6");
        (await ClientAddressAsync(byNetwork, "172.20.1.2")).Should().Be("198.51.100.7");
    }

    [Theory]
    [InlineData("localhost", HttpStatusCode.OK)]
    [InlineData("app.example.com", HttpStatusCode.OK)]
    [InlineData("evil.example.net", HttpStatusCode.BadRequest)]
    public async Task HostFiltering_AdmitsTheProbeHostBesideTheAllowlist(string host, HttpStatusCode expected)
    {
        await using var app = await StartAsync(options => options.AllowedHosts.Add("app.example.com"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client");
        request.Headers.Host = host;

        (await app.GetTestClient().SendAsync(request)).StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task HostFiltering_RejectsLocalhost_WhenProbeHostsAreCleared()
    {
        await using var app = await StartAsync(options =>
        {
            options.AllowedHosts.Add("app.example.com");
            options.ProbeHosts.Clear();
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client");
        request.Headers.Host = "localhost";

        (await app.GetTestClient().SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void AddProxyAwareHosting_RejectsAMalformedProxy()
    {
        var register = () => new ServiceCollection().AddProxyAwareHosting(options => options.TrustedProxies.Add("proxy.internal"));

        register.Should().Throw<ArgumentException>();
    }

    private static async Task<WebApplication> StartAsync(Action<ProxyAwareHostingOptions> configure)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProxyAwareHosting(configure);
        var app = builder.Build();
        app.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue(SocketHeader, out var socket))
                context.Connection.RemoteIpAddress = IPAddress.Parse(socket.ToString());
            return next(context);
        });
        app.UseProxyAwareHosting();
        app.MapGet("/client", (HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "none");
        await app.StartAsync();
        return app;
    }

    private static async Task<string> ClientAddressAsync(WebApplication app, string socket)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/client");
        request.Headers.Add(SocketHeader, socket);
        request.Headers.Add("X-Forwarded-For", "198.51.100.7");
        using var response = await app.GetTestClient().SendAsync(request);
        return await response.Content.ReadAsStringAsync();
    }
}
