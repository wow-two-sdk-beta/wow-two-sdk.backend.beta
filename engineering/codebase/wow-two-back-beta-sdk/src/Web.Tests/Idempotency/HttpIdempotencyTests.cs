using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Idempotency;

/// <summary>HTTP <c>Idempotency-Key</c> handling through an <c>AddApiDefaults</c> host, switched on by configuration.</summary>
public sealed class HttpIdempotencyTests
{
    [Fact]
    public async Task WithoutConfiguration_EveryRequestExecutes()
    {
        await using var host = await StartAsync(enabled: false);

        await host.PostAsync("key-1", new { item = "a" });
        await host.PostAsync("key-1", new { item = "a" });

        host.Executions.Should().Be(2);
    }

    [Fact]
    public async Task Enabled_ReplaysTheStoredResponseForTheSameKeyAndBody()
    {
        await using var host = await StartAsync(enabled: true);

        using var first = await host.PostAsync("key-1", new { item = "a" });
        using var retry = await host.PostAsync("key-1", new { item = "a" });
        using var other = await host.PostAsync("key-2", new { item = "a" });

        host.Executions.Should().Be(2);
        retry.StatusCode.Should().Be(HttpStatusCode.Created);
        (await retry.Content.ReadAsStringAsync()).Should().Be(await first.Content.ReadAsStringAsync());
        retry.Headers.GetValues("Idempotent-Replayed").Should().ContainSingle("true");
        first.Headers.Contains("Idempotent-Replayed").Should().BeFalse();
        (await other.Content.ReadAsStringAsync()).Should().Contain("\"order\":2");
    }

    [Fact]
    public async Task Enabled_RefusesAnotherBodyAndAConcurrentDuplicate()
    {
        await using var host = await StartAsync(enabled: true);
        await host.PostAsync("key-1", new { item = "a" });

        using var changed = await host.PostAsync("key-1", new { item = "b" });
        changed.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        host.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var slow = host.PostAsync("key-slow", new { item = "c" });
        await host.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        using var racing = await host.PostAsync("key-slow", new { item = "c" });
        racing.StatusCode.Should().Be(HttpStatusCode.Conflict);
        host.Gate.SetResult();
        (await slow).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ServerErrors_AreNotStoredSoRetriesExecuteAgain()
    {
        await using var host = await StartAsync(enabled: true);
        host.FailNext = true;

        (await host.PostAsync("key-err", new { item = "a" })).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await host.PostAsync("key-err", new { item = "a" })).StatusCode.Should().Be(HttpStatusCode.Created);

        host.Executions.Should().Be(2);
    }

    private static async Task<IdempotencyHost> StartAsync(bool enabled)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["HttpIdempotency:Enabled"] = enabled ? "true" : "false" });
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
        });
        var app = builder.Build();
        app.UseApiDefaults();
        var host = new IdempotencyHost(app);
        app.MapPost("/orders", async (HttpContext http) =>
        {
            var order = Interlocked.Increment(ref host.ExecutionCount);
            if (host.Gate is { } gate)
            {
                host.Entered.TrySetResult();
                await gate.Task;
            }

            if (host.FailNext)
            {
                host.FailNext = false;
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Results.Json(new { order }, statusCode: StatusCodes.Status201Created);
        });
        await app.StartAsync();
        return host;
    }

    private sealed class IdempotencyHost(WebApplication app) : IAsyncDisposable
    {
        public int ExecutionCount;

        public int Executions => Volatile.Read(ref ExecutionCount);

        public TaskCompletionSource? Gate { get; set; }

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool FailNext { get; set; }

        public async Task<HttpResponseMessage> PostAsync(string key, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/orders") { Content = JsonContent.Create(body) };
            request.Headers.Add("Idempotency-Key", key);
            return await app.GetTestServer().CreateClient().SendAsync(request);
        }

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
