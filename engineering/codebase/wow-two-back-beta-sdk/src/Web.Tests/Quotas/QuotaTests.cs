using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.Redis;
using WoW.Two.Sdk.Backend.Beta.Meta;
using WoW.Two.Sdk.Backend.Beta.Web.Quotas;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Quotas;

/// <summary>Usage quotas through an <c>AddApiDefaults</c> host: per-plan limits, windows, refunds and the usage read-out.</summary>
public sealed class QuotaTests
{
    [Fact]
    public async Task FreePlan_ShouldAnswer429WithRetryAfterOnceTheDailyAllowanceIsUsed()
    {
        await using var host = await StartAsync();

        var first = await host.ConvertAsync();
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Headers.GetValues("X-Quota-Limit").Single().Should().Be("2");
        first.Headers.GetValues("X-Quota-Remaining").Single().Should().Be("1");
        (await host.ConvertAsync()).Headers.GetValues("X-Quota-Remaining").Single().Should().Be("0");

        using var exhausted = await host.ConvertAsync();
        exhausted.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        exhausted.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromHours(15), "the window ends at midnight UTC");
        var problem = JsonDocument.Parse(await exhausted.Content.ReadAsStringAsync()).RootElement;
        problem.GetProperty("quota").GetString().Should().Be("conversions");
        problem.GetProperty("limit").GetInt64().Should().Be(2);
        host.Handled.Should().Be(2);

        host.Time.Advance(TimeSpan.FromHours(15));
        (await host.ConvertAsync()).StatusCode.Should().Be(HttpStatusCode.OK, "a new day is a new window");
    }

    [Fact]
    public async Task Plans_ShouldChangeTheLimit_AndSubjectsCountApart()
    {
        await using var host = await StartAsync();

        for (var call = 0; call < 5; call++)
            (await host.ConvertAsync(user: "ada", plan: "pro")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await host.ConvertAsync(user: "ada", plan: "pro")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        for (var call = 0; call < 20; call++)
            (await host.ConvertAsync(user: "bob", plan: "team")).StatusCode.Should().Be(HttpStatusCode.OK, "a negative limit is unlimited");

        (await host.ConvertAsync(user: "cy")).StatusCode.Should().Be(HttpStatusCode.OK, "users without a plan claim take the default plan");
    }

    [Fact]
    public async Task FailedCalls_ShouldRefund_AndUsageReadsBack()
    {
        await using var host = await StartAsync();

        (await host.ConvertAsync(fail: true)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await host.ConvertAsync()).StatusCode.Should().Be(HttpStatusCode.OK);

        using var usage = await host.Client.GetAsync("/quotas");
        var quota = JsonDocument.Parse(await usage.Content.ReadAsStringAsync()).RootElement.GetProperty("data")[0];
        quota.GetProperty("quota").GetString().Should().Be("conversions");
        quota.GetProperty("used").GetInt64().Should().Be(1, "the failed call was refunded");
        quota.GetProperty("remaining").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task Disabled_ShouldCountNothing()
    {
        await using var host = await StartAsync(enabled: false);

        for (var call = 0; call < 5; call++)
            (await host.ConvertAsync()).StatusCode.Should().Be(HttpStatusCode.OK);
        (await (await host.Client.GetAsync("/quotas")).Content.ReadAsStringAsync()).Should().Contain("\"data\":[]");
    }

    [Fact]
    public async Task RedisCounters_ShouldBeSharedByHosts()
    {
        await using var redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
        await redis.StartAsync();
        await using var first = await StartAsync(redis: redis.GetConnectionString());
        await using var second = await StartAsync(redis: redis.GetConnectionString());

        (await first.ConvertAsync()).StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.ConvertAsync()).StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.ConvertAsync()).StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "the other host used the second unit");
        (await second.ConvertAsync(fail: true)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>A host on a fake clock; with Redis it keeps the real clock, as Redis expires keys by its own.</summary>
    private static async Task<QuotaHost> StartAsync(bool enabled = true, string? redis = null)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.Zero));
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Quotas:Enabled"] = enabled ? "true" : "false",
            ["Quotas:Definitions:conversions:Period"] = "Day",
            ["Quotas:Definitions:conversions:Limit"] = "2",
            ["Quotas:Definitions:conversions:Plans:pro"] = "5",
            ["Quotas:Definitions:conversions:Plans:team"] = "-1",
        });
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
            options.EnableRateLimiting = false;
        });
        if (redis is null)
            builder.Services.AddSingleton<TimeProvider>(time);

        builder.Services.AddQuotas();
        if (redis is not null)
            builder.Services.AddRedisQuotaRepository(redis);

        var app = builder.Build();
        app.UseApiDefaults(pipeline => pipeline.Use((context, next) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-User", out var user))
            {
                var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.ToString()) };
                if (context.Request.Headers.TryGetValue("X-Test-Plan", out var plan))
                    claims.Add(new Claim("plan", plan.ToString()));
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            }

            return next(context);
        }));
        var host = new QuotaHost(app, time);
        app.MapPost("/convert", (bool? fail) =>
        {
            host.Handled++;
            return fail == true ? Results.StatusCode(StatusCodes.Status500InternalServerError) : Results.Ok();
        }).RequireQuota("conversions");
        app.MapQuotaUsageEndpoint();
        await app.StartAsync();
        return host;
    }

    private sealed class QuotaHost(WebApplication app, FakeTimeProvider time) : IAsyncDisposable
    {
        private HttpClient? _client;

        public FakeTimeProvider Time { get; } = time;

        public int Handled { get; set; }

        public HttpClient Client => _client ??= app.GetTestServer().CreateClient();

        public async Task<HttpResponseMessage> ConvertAsync(string? user = null, string? plan = null, bool fail = false)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, fail ? "/convert?fail=true" : "/convert");
            if (user is not null)
                request.Headers.Add("X-Test-User", user);
            if (plan is not null)
                request.Headers.Add("X-Test-Plan", plan);

            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();
            await app.DisposeAsync();
        }
    }
}
