using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WoW.Two.Sdk.Backend.Beta.Http.Hedging;
using WoW.Two.Sdk.Backend.Beta.Http.Resilience;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Http;

/// <summary>Verifies outbound replay safety, budgets, cancellation and owned-response disposal.</summary>
public sealed class HttpReplaySafetyTests
{
    [Fact]
    public async Task Retry_ShouldReplayGet_AndDisposeDiscardedResponse()
    {
        var discardedContent = new TrackingContent("retry");
        var handler = new RecordingHandler((attempt, _, _) => Task.FromResult(
            attempt == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = discardedContent }
                : new HttpResponseMessage(HttpStatusCode.OK)));
        await using var provider = BuildRetryProvider(handler, options =>
        {
            options.MaxRetryAttempts = 1;
            options.RetryDelay = TimeSpan.Zero;
        });

        using var response = await CreateClient(provider).GetAsync("resource");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Attempts.Should().Be(2);
        discardedContent.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Retry_ShouldNotReplayPostByDefault()
    {
        var handler = new RecordingHandler((_, _, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        await using var provider = BuildRetryProvider(handler, options =>
        {
            options.MaxRetryAttempts = 2;
            options.RetryDelay = TimeSpan.Zero;
        });

        using var response = await CreateClient(provider).PostAsync("resource", new StringContent("payload"));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        handler.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Retry_ShouldReplayOptedInPostWithIdenticalBody()
    {
        var bodies = new ConcurrentQueue<string>();
        var handler = new RecordingHandler(async (attempt, request, cancellationToken) =>
        {
            bodies.Enqueue(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(
                attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
        });
        await using var provider = BuildRetryProvider(handler, options =>
        {
            options.MaxRetryAttempts = 1;
            options.RetryDelay = TimeSpan.Zero;
            options.UnsafeRequestReplaySelector = request =>
                request.Headers.Contains("Idempotency-Key");
        });
        using var request = new HttpRequestMessage(HttpMethod.Post, "resource")
        {
            Content = new StringContent("payload", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", "create-1");

        using var response = await CreateClient(provider).SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bodies.Should().Equal("payload", "payload");
    }

    [Fact]
    public async Task Retry_ShouldPreserveCallerCancellationWithoutAnotherAttempt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new RecordingHandler(async (_, _, cancellationToken) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var provider = BuildRetryProvider(handler, options =>
        {
            options.MaxRetryAttempts = 3;
            options.RetryDelay = TimeSpan.Zero;
        });
        using var cancellation = new CancellationTokenSource();
        using var client = CreateClient(provider);
        var pending = client.GetAsync("resource", cancellation.Token);

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            cancellation.Cancel();
        }

        var action = () => pending;

        await action.Should().ThrowAsync<OperationCanceledException>();
        handler.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Retry_ShouldHonorTotalRequestBudget()
    {
        var handler = new RecordingHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var provider = BuildRetryProvider(handler, options =>
        {
            options.MaxRetryAttempts = 5;
            options.RetryDelay = TimeSpan.Zero;
            options.AttemptTimeout = TimeSpan.FromMilliseconds(80);
            options.TotalRequestTimeout = TimeSpan.FromMilliseconds(150);
            options.CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(1);
        });
        var stopwatch = Stopwatch.StartNew();

        var action = () => CreateClient(provider).GetAsync("resource");

        await action.Should().ThrowAsync<Exception>();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        handler.Attempts.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Hedging_ShouldRaceGet_AndDisposeLosingResponse()
    {
        var losingContent = new TrackingContent("loser");
        var handler = new RecordingHandler(async (attempt, _, _) =>
        {
            if (attempt == 1)
            {
                await Task.Delay(100, CancellationToken.None);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = losingContent };
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var provider = BuildHedgingProvider(handler, options =>
        {
            options.MaxHedgedAttempts = 1;
            options.HedgingDelay = TimeSpan.FromMilliseconds(10);
        });

        using var response = await CreateClient(provider).GetAsync("resource");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Attempts.Should().Be(2);
        losingContent.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task Hedging_ShouldNotReplayPostByDefault()
    {
        var handler = new RecordingHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(50, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var provider = BuildHedgingProvider(handler, options =>
        {
            options.MaxHedgedAttempts = 1;
            options.HedgingDelay = TimeSpan.FromMilliseconds(5);
        });

        using var response = await CreateClient(provider).PostAsync("resource", new StringContent("payload"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        handler.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task Hedging_ShouldRejectStreamContentBeforeSending()
    {
        var handler = new RecordingHandler((_, _, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)));
        await using var provider = BuildHedgingProvider(handler, _ => { });
        using var request = new HttpRequestMessage(HttpMethod.Post, "resource")
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("payload"))),
        };

        var action = () => CreateClient(provider).SendAsync(request);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Use AddSdkResilience for streaming requests*");
        handler.Attempts.Should().Be(0);
    }

    [Fact]
    public void Registration_ShouldRejectInvalidTimeoutRelationships()
    {
        var services = new ServiceCollection();

        var action = () => services.AddHttpClient("invalid").AddSdkResilience(options =>
        {
            options.AttemptTimeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout = TimeSpan.FromSeconds(5);
        });

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static ServiceProvider BuildRetryProvider(
        HttpMessageHandler handler,
        Action<HttpResilienceOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test", client => client.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddSdkResilience(configure);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildHedgingProvider(
        HttpMessageHandler handler,
        Action<HttpHedgingOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("test", client => client.BaseAddress = new Uri("https://example.test"))
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddSdkHedging(configure);
        return services.BuildServiceProvider();
    }

    private static HttpClient CreateClient(ServiceProvider provider) =>
        provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

    private sealed class RecordingHandler(
        Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        private int _attempts;

        public int Attempts => Volatile.Read(ref _attempts);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await send(Interlocked.Increment(ref _attempts), request, cancellationToken);
            response.RequestMessage = request;
            return response;
        }
    }

    private sealed class TrackingContent(string value)
        : ByteArrayContent(Encoding.UTF8.GetBytes(value))
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
