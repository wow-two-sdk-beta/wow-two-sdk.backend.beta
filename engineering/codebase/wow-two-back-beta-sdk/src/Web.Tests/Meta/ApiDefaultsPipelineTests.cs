using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Backend.Beta.Meta;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.Meta;

/// <summary>Exercises the complete API-default middleware order over an in-memory host.</summary>
public sealed class ApiDefaultsPipelineTests
{
    private const string AuthenticationScheme = "test";

    private sealed record PipelineMarker;

    private sealed class PipelineObservation
    {
        public bool RateLimiterSawEndpoint { get; set; }

        public string? RateLimiterSubject { get; set; }

        public IList<string?> CacheSubjects { get; } = [];
    }

    private sealed class HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var subject = Request.Headers["X-Test-Subject"].ToString();
            if (string.IsNullOrWhiteSpace(subject))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, subject)],
                AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class IdentityOutputCachePolicy(PipelineObservation observation) : IOutputCachePolicy
    {
        public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellationToken)
        {
            var subject = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            observation.CacheSubjects.Add(subject);
            context.EnableOutputCaching = true;
            context.AllowCacheLookup = true;
            context.AllowCacheStorage = true;
            context.AllowLocking = true;
            context.CacheVaryByRules.VaryByValues["subject"] = subject ?? "anonymous";

            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }

    private static async Task<(WebApplication App, PipelineObservation Observation)> StartHostAsync()
    {
        var observation = new PipelineObservation();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.AddApiDefaults(options =>
        {
            options.EnableHttpsRedirection = false;
            options.EnableOtlpExporters = false;
            options.ExposeOpenApi = false;
        });
        builder.Services
            .AddAuthentication(AuthenticationScheme)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(AuthenticationScheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.Configure<RateLimiterOptions>(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                observation.RateLimiterSawEndpoint =
                    context.GetEndpoint()?.Metadata.GetMetadata<PipelineMarker>() is not null;
                observation.RateLimiterSubject =
                    context.User.FindFirstValue(ClaimTypes.NameIdentifier);

                return RateLimitPartition.GetNoLimiter("global");
            });
        });
        builder.Services.AddOutputCache(options =>
            options.AddPolicy("identity", new IdentityOutputCachePolicy(observation)));

        var app = builder.Build();
        app.UseApiDefaults(pipeline =>
        {
            pipeline.UseAuthentication();
            pipeline.UseAuthorization();
        });

        var executionCount = 0;
        app.MapGet("/secured", (ClaimsPrincipal user) =>
                $"{user.FindFirstValue(ClaimTypes.NameIdentifier)}:{Interlocked.Increment(ref executionCount)}")
            .WithMetadata(new PipelineMarker())
            .RequireAuthorization()
            .CacheOutput("identity");
        app.MapGet("/compressed", () => Results.Text(new string('x', 4096), "text/plain"));

        await app.StartAsync();

        return (app, observation);
    }

    [Fact]
    public async Task IdentitySeam_ShouldRunAfterRouting_AndBeforeAuthorizationAndRateLimiting()
    {
        var (app, observation) = await StartHostAsync();
        await using (app)
        using (var client = app.GetTestClient())
        {
            client.DefaultRequestHeaders.Add("X-Test-Subject", "alice");

            using var response = await client.GetAsync("/secured");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            observation.RateLimiterSawEndpoint.Should().BeTrue();
            observation.RateLimiterSubject.Should().Be("alice");
        }
    }

    [Fact]
    public async Task OutputCachePolicy_ShouldVaryByAuthenticatedIdentity()
    {
        var (app, observation) = await StartHostAsync();
        await using (app)
        {
            using var alice = app.GetTestClient();
            alice.DefaultRequestHeaders.Add("X-Test-Subject", "alice");
            using var bob = app.GetTestClient();
            bob.DefaultRequestHeaders.Add("X-Test-Subject", "bob");

            (await alice.GetStringAsync("/secured")).Should().Be("alice:1");
            (await alice.GetStringAsync("/secured")).Should().Be("alice:1");
            (await bob.GetStringAsync("/secured")).Should().Be("bob:2");
            observation.CacheSubjects.Should().OnlyContain(subject => subject == "alice" || subject == "bob");
        }
    }

    [Fact]
    public async Task ResponseCompression_ShouldWrapEndpointResponses()
    {
        var (app, _) = await StartHostAsync();
        await using (app)
        using (var client = app.GetTestClient())
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/compressed"))
        {
            request.Headers.AcceptEncoding.ParseAdd("gzip");

            using var response = await client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentEncoding.Should().ContainSingle().Which.Should().Be("gzip");
        }
    }
}
