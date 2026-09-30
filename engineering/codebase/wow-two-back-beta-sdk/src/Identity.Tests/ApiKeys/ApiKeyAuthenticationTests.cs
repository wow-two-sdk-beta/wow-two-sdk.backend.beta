using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Identity.ApiKeys;

namespace WoW.Two.Sdk.Backend.Beta.Identity.Tests.ApiKeys;

/// <summary>Drives the API key scheme and gate over an in-memory host with a dictionary store.</summary>
public sealed class ApiKeyAuthenticationTests
{
    private const string Marker = "tf_";
    private const string RemoteHeader = "X-Test-Remote";

    [Fact]
    public void Create_ShouldMintAMarkedSecret_WhoseHashAndPrefixAreStored()
    {
        var factory = new ApiKeySecretFactory(Options.Create(new ApiKeyOptions { Marker = Marker }));

        var key = factory.Create();

        key.Secret.Should().StartWith(Marker).And.HaveLength(Marker.Length + 32);
        key.Prefix.Should().Be(key.Secret[..11]);
        key.Hash.Should().MatchRegex("^[0-9a-f]{64}$").And.Be(ApiKeySecretFactory.ToHash(key.Secret));
        factory.IsSecretShaped(key.Secret).Should().BeTrue();
        factory.IsSecretShaped(Marker + "short").Should().BeFalse();
        factory.IsSecretShaped(Marker + new string('!', 32)).Should().BeFalse();
    }

    [Fact]
    public async Task Gate_ShouldLetThisMachineThrough_WhenNoKeyIsPresented()
    {
        await using var host = await Host.StartAsync();

        using var response = await host.Client.GetAsync(new Uri("/api/who", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("anonymous");
    }

    [Fact]
    public async Task Gate_ShouldAnswer401_WhenARemoteCallerPresentsNoKey()
    {
        await using var host = await Host.StartAsync();

        using var response = await host.Client.SendAsync(Remote("/api/who"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Gate_ShouldAuthenticateARemoteCaller_WhenALiveKeyIsPresentedEitherWay()
    {
        await using var host = await Host.StartAsync();
        var key = host.AddKey("Research notebook");

        using var bearer = await host.Client.SendAsync(Remote("/api/who", bearer: key.Secret));
        using var header = await host.Client.SendAsync(Remote("/api/who", header: key.Secret));

        bearer.StatusCode.Should().Be(HttpStatusCode.OK);
        (await bearer.Content.ReadAsStringAsync()).Should().Be("Research notebook");
        header.StatusCode.Should().Be(HttpStatusCode.OK);
        host.Store.Touches.Should().ContainSingle("the second use falls inside the touch interval");
    }

    [Fact]
    public async Task Gate_ShouldAnswer401_WhenAnUnknownKeyIsPresentedEvenLocally()
    {
        await using var host = await Host.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/who");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Marker + new string('a', 32));
        using var response = await host.Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Gate_ShouldAnswer403_WhenAKeyReachesALocalOnlyPath()
    {
        await using var host = await Host.StartAsync();
        var key = host.AddKey("Script");

        using var response = await host.Client.SendAsync(Remote("/api/keys", bearer: key.Secret));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Gate_ShouldLeaveOpenAndUnguardedPathsAlone_WhenARemoteCallerPresentsNoKey()
    {
        await using var host = await Host.StartAsync();

        using var open = await host.Client.SendAsync(Remote("/api/status"));
        using var unguarded = await host.Client.SendAsync(Remote("/page"));

        open.StatusCode.Should().Be(HttpStatusCode.OK);
        unguarded.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Scheme_ShouldIgnoreABearerWithoutTheMarker_SoOtherSchemesCanReadIt()
    {
        await using var host = await Host.StartAsync();

        using var local = new HttpRequestMessage(HttpMethod.Get, "/api/who");
        local.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.e30.sig");
        using var localResponse = await host.Client.SendAsync(local);
        using var remoteResponse = await host.Client.SendAsync(Remote("/api/who", bearer: "eyJhbGciOiJIUzI1NiJ9.e30.sig"));

        localResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        remoteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        host.Store.Lookups.Should().Be(0);
    }

    [Fact]
    public async Task Scheme_ShouldCarryEachScopeTheKeyGrants_AsScopeClaims()
    {
        await using var host = await Host.StartAsync();
        var key = host.AddKey("Catalog reader", "catalog:read", "catalog:read", "logs:read");

        using var response = await host.Client.SendAsync(Remote("/api/scopes", bearer: key.Secret));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("catalog:read logs:read");
    }

    [Fact]
    public async Task RequireApiKeyScope_ShouldAdmitAKeyWithTheScope_AndForbidOneWithout()
    {
        await using var host = await Host.StartAsync();
        var reader = host.AddKey("Catalog reader", "catalog:read");
        var other = host.AddKey("Log reader", "logs:read");

        using var admitted = await host.Client.SendAsync(Remote("/api/catalog", bearer: reader.Secret));
        using var forbidden = await host.Client.SendAsync(Remote("/api/catalog", bearer: other.Secret));

        admitted.StatusCode.Should().Be(HttpStatusCode.OK);
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void HasApiKeyScope_ShouldIgnoreAScopeClaim_WhenAnotherSchemeIssuedIt()
    {
        var cookie = new ClaimsIdentity([new Claim(ApiKeyAuthenticationDefaults.ScopeClaim, "catalog:read")], "Cookies");
        var key = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "Script"), new Claim(ApiKeyAuthenticationDefaults.ScopeClaim, "logs:read")],
            ApiKeyAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal([cookie, key]);

        principal.HasApiKeyScope("catalog:read").Should().BeFalse();
        principal.HasApiKeyScope("logs:read").Should().BeTrue();
        principal.GetApiKeyName().Should().Be("Script");
    }

    /// <summary>Builds a request the host sees as coming from another machine.</summary>
    private static HttpRequestMessage Remote(string path, string? bearer = null, string? header = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(RemoteHeader, "203.0.113.9");
        if (bearer is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        if (header is not null)
            request.Headers.Add(ApiKeyAuthenticationDefaults.HeaderName, header);
        return request;
    }

    /// <summary>Holds an in-memory host with the scheme, the gate and a dictionary store.</summary>
    private sealed class Host(WebApplication app, DictionaryApiKeyRepository store, ApiKeySecretFactory secrets) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();

        public DictionaryApiKeyRepository Store => store;

        public static async Task<Host> StartAsync()
        {
            var store = new DictionaryApiKeyRepository();
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IApiKeyRepository>(store);
            builder.Services.AddAuthorization(options =>
                options.AddPolicy("catalog", policy => policy.RequireApiKeyScope("catalog:read")));
            builder.Services.AddApiKeyAuthentication(
                keys => keys.Marker = Marker,
                gate =>
                {
                    gate.OpenPaths.Add("/api/status");
                    gate.LocalOnlyPaths.Add("/api/keys");
                });

            var app = builder.Build();
            app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue(RemoteHeader, out var address))
                    context.Connection.RemoteIpAddress = IPAddress.Parse(address.ToString());
                return next(context);
            });
            app.UseAuthentication();
            app.UseApiKeyAccessGate();
            app.UseAuthorization();
            app.MapGet("/api/who", (ClaimsPrincipal user) => user.Identity?.Name ?? "anonymous");
            app.MapGet("/api/scopes", (ClaimsPrincipal user) =>
                string.Join(' ', user.FindAll(ApiKeyAuthenticationDefaults.ScopeClaim).Select(claim => claim.Value)));
            app.MapGet("/api/catalog", () => "catalog").RequireAuthorization("catalog");
            app.MapGet("/api/keys", () => "keys");
            app.MapGet("/api/status", () => "ok");
            app.MapGet("/page", () => "page");
            await app.StartAsync();
            return new Host(app, store, app.Services.GetRequiredService<ApiKeySecretFactory>());
        }

        public ApiKeySecret AddKey(string name, params string[] scopes)
        {
            var key = secrets.Create();
            store.Keys[key.Hash] = new ApiKeyRecord { Id = Guid.NewGuid().ToString(), Name = name, Scopes = scopes };
            return key;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    /// <summary>Stands in for a product's key table.</summary>
    private sealed class DictionaryApiKeyRepository : IApiKeyRepository
    {
        public Dictionary<string, ApiKeyRecord> Keys { get; } = [];

        public List<string> Touches { get; } = [];

        public int Lookups { get; private set; }

        public Task<ApiKeyRecord?> FindLiveByHashAsync(string hash, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(Keys.GetValueOrDefault(hash));
        }

        public Task TouchAsync(string id, DateTimeOffset usedAt, CancellationToken cancellationToken)
        {
            Touches.Add(id);
            foreach (var (hash, record) in Keys.Where(pair => pair.Value.Id == id).ToList())
                Keys[hash] = record with { LastUsedAt = usedAt };
            return Task.CompletedTask;
        }
    }
}
