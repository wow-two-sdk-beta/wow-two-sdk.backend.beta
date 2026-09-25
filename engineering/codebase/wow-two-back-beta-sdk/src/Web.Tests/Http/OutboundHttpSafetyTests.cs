using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Http.Safety;
using WoW.Two.Sdk.Backend.Beta.Http.Safety.Validators;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Http;

public sealed class OutboundHttpSafetyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2002:7f00:1::")]
    [InlineData("2001:db8::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    public void AddressPolicyRejectsNonPublicAndTransitionAddresses(string address)
    {
        Assert.False(new OutboundAddressValidator().IsAllowed(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void AddressPolicyAcceptsPublicAddresses(string address)
    {
        Assert.True(new OutboundAddressValidator().IsAllowed(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("http://example.com/")]
    [InlineData("https://other.invalid/")]
    [InlineData("https://user:secret@example.com/")]
    public async Task RequestPolicyRejectsBeforeSending(string address)
    {
        await using var provider = Build(options => options.AllowedHosts.Add("example.com"));
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("safe");
        await Assert.ThrowsAsync<OutboundAddressBlockedException>(() => client.GetAsync(address));
    }

    [Fact]
    public async Task DefaultTransportRejectsLoopback()
    {
        await using var provider = Build();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("safe");
        HttpRequestException error = await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.GetAsync("https://127.0.0.1:9/"));
        Assert.True(error is OutboundAddressBlockedException || error.InnerException is OutboundAddressBlockedException);
    }

    [Fact]
    public async Task RedirectIsReturnedWithoutContactingItsDestination()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Task server = RespondAsync(listener, timeout.Token);
        await using var provider = Build(options =>
        {
            options.RequireHttps = false;
            options.AllowPrivateNetworkTargets = true;
            options.AllowedHosts.Add("127.0.0.1");
        });
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("safe");
        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"http://127.0.0.1:{port}/"),
            timeout.Token);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        await server;
    }

    [Fact]
    public async Task Http3CannotBypassTheConnectionGuard()
    {
        await using var provider = Build();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("safe");
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com")
        {
            Version = HttpVersion.Version30,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(request));
    }

    [Fact]
    public async Task RegistrationDisablesProxyAndCookieRouting()
    {
        await using var provider = Build();
        HttpMessageHandler handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("safe");
        while (handler is DelegatingHandler delegating)
        {
            handler = delegating.InnerHandler!;
        }
        var sockets = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(sockets.UseProxy);
        Assert.False(sockets.UseCookies);
        Assert.NotNull(sockets.ConnectCallback);
    }

    private static ServiceProvider Build(Action<OutboundHttpOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("safe").AddSafeOutboundHttp(configure);
        return services.BuildServiceProvider();
    }

    private static async Task RespondAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using TcpClient socket = await listener.AcceptTcpClientAsync(cancellationToken);
        await using NetworkStream stream = socket.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 })
        {
        }
        byte[] response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 307 Temporary Redirect\r\nLocation: http://blocked.invalid/\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, cancellationToken);
    }
}
