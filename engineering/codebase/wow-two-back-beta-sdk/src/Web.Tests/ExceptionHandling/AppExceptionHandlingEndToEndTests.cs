using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Errors;
using WoW.Two.Sdk.Backend.Beta.Foundation.Validation;
using WoW.Two.Sdk.Backend.Beta.Web.ExceptionHandling;
using WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;
using Xunit;

namespace WoW.Two.Sdk.Backend.Beta.Web.Tests.ExceptionHandling;

/// <summary>
/// End-to-end coverage of the SDK exception pipeline: a throw inside an endpoint flows through
/// <c>UseExceptionHandler</c> and the registered <see cref="IExceptionHandler"/> chain
/// (<see cref="ValidationExceptionHandler"/> -&gt; <see cref="AppExceptionHandler"/> -&gt;
/// <see cref="UnhandledExceptionHandler"/>) plus the shared factory, emitting RFC 9457
/// <c>application/problem+json</c>. Proves <c>AddAppExceptionHandling</c> wires every seam
/// (status mapper, message resolver, observer, problem-details service) over a real host.
/// </summary>
public sealed class AppExceptionHandlingEndToEndTests
{
    private const string ProblemJsonContentType = "application/problem+json";

    /// <summary>A domain exception the SDK does not know — mapped only by the app-contributed rule below.</summary>
    private sealed class CustomDomainException : Exception;

    /// <summary>An app-registered rule that maps <see cref="CustomDomainException"/> to a NotFound error.</summary>
    private sealed class CustomDomainExceptionRule : IExceptionMappingRule
    {
        public AppError? TryMap(Exception exception)
            => exception is CustomDomainException ? AppErrors.NotFound("custom mapped") : null;
    }

    /// <summary>Boots an in-memory TestServer host wired exactly as <c>AddApiDefaults</c> wires the error pipeline, with one endpoint per failure shape; <paramref name="configure"/> registers extra mapping rules.</summary>
    private static async Task<WebApplication> StartPipelineHostAsync(Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services
            .AddTraceAwareProblemDetails()
            .AddAppExceptionHandling();

        configure?.Invoke(builder.Services);

        var app = builder.Build();

        // Outermost, exactly as UseApiDefaults wires it — routes throws through the registered handlers.
        app.UseExceptionHandler();

        app.MapGet("/throw/app-notfound", void () => AppErrors.NotFound("Order 3f2 not found.").Throw());
        app.MapGet("/throw/validation", void () => throw new ValidationException(ValidationError.From(
        [
            new FieldError { Property = "email", Message = "Email is required.", Code = "NotEmptyValidator" },
        ])));
        app.MapGet("/throw/too-many", void () => AppError.Of(
            AppErrorType.TooManyRequests,
            "Slow down.",
            new Dictionary<string, object?> { ["retryAfter"] = "30" }).Throw());
        app.MapGet("/throw/unhandled", void () => throw new InvalidOperationException("secret connection string leaked"));
        app.MapGet("/throw/db-conflict", void () => throw new PostgresException("duplicate key", "ERROR", "ERROR", "23505"));
        app.MapGet("/throw/custom", void () => throw new CustomDomainException());

        await app.StartAsync();

        return app;
    }

    private static async Task<(HttpResponseMessage Response, string Raw, JsonElement Body)> GetProblemAsync(WebApplication app, string path)
    {
        using var client = app.GetTestClient();

        var response = await client.GetAsync(new Uri(path, UriKind.Relative));
        var raw = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(raw);

        return (response, raw, document.RootElement.Clone());
    }

    [Fact]
    public async Task AppException_ShouldRenderNotFoundProblemDetails_WhenThrownFromEndpoint()
    {
        await using var app = await StartPipelineHostAsync();

        var (response, _, body) = await GetProblemAsync(app, "/throw/app-notfound");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJsonContentType);
        body.GetProperty("type").GetString().Should().Be("urn:wow-two:error:NotFound");
        body.GetProperty("status").GetInt32().Should().Be(StatusCodes.Status404NotFound);
        body.GetProperty("code").GetString().Should().Be("NotFound");
        body.GetProperty("detail").GetString().Should().Be("Order 3f2 not found.");

        // The trace-aware customization must still run for handler-produced ProblemDetails.
        body.TryGetProperty("requestId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ValidationException_ShouldRender400WithFieldErrors_WhenThrownFromEndpoint()
    {
        await using var app = await StartPipelineHostAsync();

        var (response, _, body) = await GetProblemAsync(app, "/throw/validation");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJsonContentType);
        body.GetProperty("code").GetString().Should().Be("Validation");
        body.GetProperty("title").GetString().Should().Be("One or more validation errors occurred.");

        var errors = body.GetProperty("errors");
        errors.GetArrayLength().Should().Be(1);
        errors[0].GetProperty("property").GetString().Should().Be("email");
        errors[0].GetProperty("code").GetString().Should().Be("NotEmptyValidator");
    }

    [Fact]
    public async Task AppException_ShouldPromoteRetryAfterHeader_WhenErrorCarriesReservedMetadata()
    {
        await using var app = await StartPipelineHostAsync();

        var (response, _, body) = await GetProblemAsync(app, "/throw/too-many");

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        body.GetProperty("code").GetString().Should().Be("TooManyRequests");

        response.Headers.TryGetValues("Retry-After", out var retryAfter).Should().BeTrue();
        retryAfter!.Should().ContainSingle().Which.Should().Be("30");
    }

    [Fact]
    public async Task UnhandledException_ShouldRenderSafe500_WithoutLeakingExceptionDetail()
    {
        await using var app = await StartPipelineHostAsync();

        var (response, raw, body) = await GetProblemAsync(app, "/throw/unhandled");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJsonContentType);
        body.GetProperty("type").GetString().Should().Be("urn:wow-two:error:Unexpected");
        body.GetProperty("code").GetString().Should().Be("Unexpected");
        body.GetProperty("detail").GetString().Should().Be("An unexpected error occurred.");
        raw.Should().NotContain("secret connection string leaked");
    }

    [Fact]
    public async Task DbException_ShouldMapToConflict_WhenPersistenceRuleRegistered()
    {
        // AddPostgresPersistence wires this rule; here we register it directly to isolate the mapping.
        await using var app = await StartPipelineHostAsync(services => services.AddDbExceptionMapping());

        var (response, _, body) = await GetProblemAsync(app, "/throw/db-conflict");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body.GetProperty("type").GetString().Should().Be("urn:wow-two:error:Conflict");
        body.GetProperty("code").GetString().Should().Be("Conflict");
    }

    [Fact]
    public async Task AppRegisteredRule_ShouldMapException_WhenSdkDoesNotKnowIt()
    {
        await using var app = await StartPipelineHostAsync(
            services => services.AddExceptionMappingRule(new CustomDomainExceptionRule()));

        var (response, _, body) = await GetProblemAsync(app, "/throw/custom");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.GetProperty("code").GetString().Should().Be("NotFound");
    }
}
